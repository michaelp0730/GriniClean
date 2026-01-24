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

        // Register commands/services later (modules will plug in here)
        services.AddSingleton<CacheScanCommand>();

        var registrar = new TypeRegistrar(services);
        var app = new CommandApp(registrar);

        app.Configure(config =>
        {
            // Make help text show "gc" if invoked as gc
            var invokedAs = Path.GetFileName(Environment.GetCommandLineArgs()[0]);
            var name = string.Equals(invokedAs, "gc", StringComparison.OrdinalIgnoreCase) ? "gc" : "gc";
            config.SetApplicationName(name);

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

// Dummy command to confirm plumbing
internal sealed class CacheScanCommand : Command<CacheScanCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        // Optional: add flags later
        [Description("If set, does not calculate folder sizes.")]
        [CommandOption("--fast")]
        public bool Fast { get; init; }
    }

    public override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        AnsiConsole.MarkupLine("[green]gc is running.[/]");
        AnsiConsole.MarkupLine("Next: implement cache scanning under ~/Library/Caches");
        return 0;
    }
}
