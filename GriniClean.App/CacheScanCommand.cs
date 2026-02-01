using GriniClean.Core.Models;
using GriniClean.Modules.Cache.Services;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace GriniClean;

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

        [Description("Minimum size to display (e.g. 1MB, 500KB, 2GB). Default: 1MB.")]
        [CommandOption("--min-size <SIZE>")]
        public string? MinSize { get; init; }

        [Description("Include zero-byte targets.")]
        [CommandOption("--show-zero")]
        public bool ShowZero { get; init; }

        [Description("Show only the top N targets by size.")]
        [CommandOption("--top <N>")]
        public int? Top { get; init; }

        [Description("Include Apple user caches (com.apple.*). Off by default.")]
        [CommandOption("--include-apple")]
        public bool IncludeApple { get; init; }
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

        var minBytes = ParseSizeOrDefault(settings.MinSize, defaultBytes: 1024L * 1024); // 1MB default

        var filtered = targets.Where(t =>
        {
            if (!settings.IncludeApple && t.IsApple) return false;
            var size = t.SizeBytes ?? 0;
            if (!settings.ShowZero && size == 0) return false;
            return size >= minBytes;
        });

        if (settings.Top.HasValue && settings.Top.Value > 0)
        {
            filtered = filtered.OrderByDescending(t => t.SizeBytes ?? -1).Take(settings.Top.Value);
        }

        var list = filtered.ToList();

        foreach (var t in list)
        {
            var kind = t.Kind switch
            {
                CacheTargetKind.UserCachesRootChild => "User cache",
                CacheTargetKind.ContainerCaches => "Container cache",
                _ => "Unknown"
            };

            if (t.IsAdvanced) kind += " (adv)";
            if (t.IsApple) kind += " (Apple)";

            var size = t.SizeBytes.HasValue ? FormatBytes(t.SizeBytes.Value) : (settings.Fast ? "-" : "n/a");

            table.AddRow(
                EscapeMarkup(t.DisplayName),
                EscapeMarkup(kind),
                EscapeMarkup(size),
                EscapeMarkup(t.Path)
            );
        }

        AnsiConsole.Write(table);

        AnsiConsole.MarkupLine($"\nFound [green]{list.Count}[/] targets.");
        if (!settings.IncludeContainers)
        {
            AnsiConsole.MarkupLine("[grey]Tip: use --include-containers to see sandbox container caches (advanced).[/]");
        }

        if (!settings.IncludeApple)
        {
            AnsiConsole.MarkupLine("[grey]Tip: use --include-apple to include Apple user caches (com.apple.*).[/]");
        }

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

    private static long ParseSizeOrDefault(string? text, long defaultBytes)
    {
        if (string.IsNullOrWhiteSpace(text))
            return defaultBytes;

        var s = text.Trim().ToUpperInvariant();

        // allow "1MB", "1 MB", etc.
        s = s.Replace(" ", "");

        long multiplier =
            s.EndsWith("TB") ? 1024L * 1024 * 1024 * 1024 :
            s.EndsWith("GB") ? 1024L * 1024 * 1024 :
            s.EndsWith("MB") ? 1024L * 1024 :
            s.EndsWith("KB") ? 1024L :
            s.EndsWith("B")  ? 1L :
            0;

        var numberPart = multiplier == 0
            ? s
            : s[..^2]; // remove unit (KB/MB/GB/TB) — except "B" case handled above

        if (multiplier == 0 && s.EndsWith("B"))
        {
            multiplier = 1;
            numberPart = s[..^1];
        }

        if (multiplier == 0)
            throw new ArgumentException($"Unrecognized size '{text}'. Use KB, MB, GB, or TB.");

        if (!double.TryParse(numberPart, out var value) || value < 0)
            throw new ArgumentException($"Invalid size '{text}'.");

        var bytes = (long)(value * multiplier);
        return bytes;
    }
}
