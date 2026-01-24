using GriniClean.Core.Models;
using GriniClean.Infrastructure.FileSystem;
using GriniClean.Infrastructure.OS;
using GriniClean.Modules.Cache.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace GriniClean;

public static class Program
{
    public static int Main(string[] args)
    {
        var services = new ServiceCollection();

        services.AddLogging(b =>
        {
            b.ClearProviders();
            b.AddSimpleConsole(o =>
            {
                o.SingleLine = true;
                o.TimestampFormat = "HH:mm:ss ";
            });
            b.SetMinimumLevel(LogLevel.Information);
        });

        // Infrastructure
        services.AddSingleton<IFileSystem, OsFileSystem>();
        services.AddSingleton<IUserPaths, MacUserPaths>();

        // Cache module
        services.AddSingleton<ICacheScanner, MacCacheScanner>();

        // Commands
        services.AddSingleton<CacheScanCommand>();

        var registrar = new TypeRegistrar(services);
        var app = new CommandApp(registrar);

        app.Configure(config =>
        {
            config.SetApplicationName("gc");

            config.AddCommand<CacheScanCommand>("cache-scan")
                .WithDescription("Scan safe user cache locations (no system directories).");
        });

        return app.Run(args);
    }
}

// Simple Spectre DI registrar
internal sealed class TypeRegistrar(IServiceCollection builder) : ITypeRegistrar
{
    public ITypeResolver Build() => new TypeResolver(builder.BuildServiceProvider());
    public void Register(Type service, Type implementation) => builder.AddSingleton(service, implementation);
    public void RegisterInstance(Type service, object implementation) => builder.AddSingleton(service, implementation);
    public void RegisterLazy(Type service, Func<object> factory) => builder.AddSingleton(service, _ => factory());

    private sealed class TypeResolver(IServiceProvider provider) : ITypeResolver, IDisposable
    {
        public object? Resolve(Type? type) => type is null ? null : provider.GetService(type);
        public void Dispose() { if (provider is IDisposable d) d.Dispose(); }
    }
}

internal sealed class CacheScanCommand(ICacheScanner scanner) : Command<CacheScanCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [Description("If set, does not calculate folder sizes (faster).")]
        [CommandOption("--fast")]
        public bool Fast { get; init; }

        [Description("Include sandbox container caches under ~/Library/Containers (advanced).")]
        [CommandOption("--include-containers")]
        public bool IncludeContainers { get; init; }

        [Description("Print diagnostic info about scanned locations.")]
        [CommandOption("--verbose")]
        public bool Verbose { get; init; }

    }

    public override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var options = new CacheScanOptions(
            Fast: settings.Fast,
            IncludeContainers: settings.IncludeContainers
        );

        AnsiConsole.MarkupLine("[bold]Scanning caches...[/]");

        IReadOnlyList<CacheTarget> targets;
        try
        {
            if (settings.Verbose)
            {
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (string.IsNullOrWhiteSpace(home))
                {
                    home = Environment.GetEnvironmentVariable("HOME") ?? "";
                }

                AnsiConsole.MarkupLine($"[grey]Home:[/] {Markup.Escape(home)}");
                AnsiConsole.MarkupLine($"[grey]Checking:[/] {Markup.Escape(
                    Path.Combine(home, "Library", "Caches"))}");

                if (settings.IncludeContainers)
                {
                    AnsiConsole.MarkupLine($"[grey]Checking:[/] {Markup.Escape(
                        Path.Combine(home, "Library", "Containers"))}");
                }
            }

            targets = scanner.Scan(options, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            AnsiConsole.MarkupLine("[yellow]Canceled.[/]");
            return 130;
        }

        if (targets.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No cache targets found (or access denied).[/]");
            return 0;
        }

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn(new TableColumn("Target").LeftAligned())
            .AddColumn(new TableColumn("Kind").LeftAligned())
            .AddColumn(new TableColumn("Size").RightAligned())
            .AddColumn(new TableColumn("Path").LeftAligned());

        foreach (var t in targets)
        {
            var kind = t.Kind switch
            {
                CacheTargetKind.UserCachesRootChild => "User cache",
                CacheTargetKind.ContainerCaches => "Container cache",
                _ => "Unknown"
            };

            if (t.IsAdvanced)
                kind += " (adv)";

            var size = t.SizeBytes.HasValue ? FormatBytes(t.SizeBytes.Value) : (settings.Fast ? "-" : "n/a");

            table.AddRow(
                EscapeMarkup(t.DisplayName),
                EscapeMarkup(kind),
                EscapeMarkup(size),
                EscapeMarkup(t.Path)
            );
        }

        AnsiConsole.Write(table);

        AnsiConsole.MarkupLine($"\nFound [green]{targets.Count}[/] targets.");
        if (!settings.IncludeContainers)
            AnsiConsole.MarkupLine("[grey]Tip: use --include-containers to see sandbox container caches (advanced).[/]");

        return 0;
    }

    private static string FormatBytes(long bytes)
    {
        // simple human readable
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double size = bytes;
        int unit = 0;

        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }

        return unit == 0 ? $"{bytes} {units[unit]}" : $"{size:0.##} {units[unit]}";
    }

    private static string EscapeMarkup(string s) => Markup.Escape(s);
}
