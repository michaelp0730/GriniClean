using System.ComponentModel;
using GriniClean.Core.Models;
using GriniClean.Modules.Cache.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace GriniClean;

internal sealed class CacheCleanCommand(ICacheScanner scanner, ICacheCleaner cleaner)
    : Command<CacheCleanCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [Description("If set, does not calculate folder sizes (faster).")]
        [CommandOption("--fast")]
        public bool Fast { get; init; }

        [Description("Include sandbox container caches under ~/Library/Containers (advanced).")]
        [CommandOption("--include-containers")]
        public bool IncludeContainers { get; init; }

        [Description("Simulate actions without moving anything to Trash.")]
        [CommandOption("--dry-run")]
        public bool DryRun { get; init; }

        [Description("Selection mode: 'select' (multi-select list) or 'prompt' (yes/no per item). Default: select.")]
        [CommandOption("--mode <MODE>")]
        public string? Mode { get; init; }

        [Description("Only include targets at or above this size (e.g. 1MB, 500KB, 2GB). Default: 1MB.")]
        [CommandOption("--min-size <SIZE>")]
        public string? MinSize { get; init; }

        [Description("Include zero-byte targets.")]
        [CommandOption("--show-zero")]
        public bool ShowZero { get; init; }

        [Description("If specified, only targets whose name/path contains this text (case-insensitive).")]
        [CommandOption("--filter <TEXT>")]
        public string? Filter { get; init; }

        [CommandOption("--include-apple")]
        [Description("Include Apple user caches (com.apple.*). Off by default.")]
        public bool IncludeApple { get; init; }
    }

    public override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var mode = (settings.Mode ?? "select").Trim().ToLowerInvariant();
        if (mode is not ("select" or "prompt"))
        {
            AnsiConsole.MarkupLine("[red]Invalid --mode.[/] Use 'select' or 'prompt'.");
            return 2;
        }

        var minBytes = ParseSizeOrDefault(settings.MinSize, 1024L * 1024); // 1MB default
        var targets = scanner.Scan(
            new CacheScanOptions(Fast: settings.Fast, IncludeContainers: settings.IncludeContainers),
            cancellationToken
        );

        var filtered = targets.Where(t =>
            {
                var size = t.SizeBytes ?? 0;

                if (!settings.IncludeApple && t.IsApple) return false;
                if (!settings.ShowZero && size == 0) return false;
                if (size < minBytes) return false;

                if (!string.IsNullOrWhiteSpace(settings.Filter))
                {
                    var f = settings.Filter.Trim();
                    if (!t.DisplayName.Contains(f, StringComparison.OrdinalIgnoreCase) &&
                        !t.Path.Contains(f, StringComparison.OrdinalIgnoreCase))
                        return false;
                }

                return true;
            })
            .OrderByDescending(t => t.SizeBytes ?? -1)
            .ThenBy(t => t.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (filtered.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No cache targets matched your filters.[/]");
            return 0;
        }

        // 1) Choose targets
        List<CacheTarget> selected = mode == "prompt"
            ? PromptPerItem(filtered)
            : MultiSelect(filtered, includeContainers: settings.IncludeContainers);

        if (selected.Count == 0)
        {
            AnsiConsole.MarkupLine("[grey]No selections. Nothing to do.[/]");
            return 0;
        }

        // 2) Confirm
        var totalBytes = selected.Sum(t => t.SizeBytes ?? 0);
        var totalStr = settings.Fast ? "unknown" : FormatBytes(totalBytes);

        if (settings.DryRun)
        {
            AnsiConsole.MarkupLine(
                $"[yellow]DRY RUN[/]: would move [green]{selected.Count}[/] targets (~{Markup.Escape(totalStr)}) to Trash."
            );
            foreach (var t in selected)
                AnsiConsole.MarkupLine($"  [grey]-[/] {Markup.Escape(t.Path)}");
            return 0;
        }

        if (!AnsiConsole.Confirm($"Move [green]{selected.Count}[/] targets (~{Markup.Escape(totalStr)}) to Trash?", defaultValue: false))
        {
            AnsiConsole.MarkupLine("[grey]Canceled.[/]");
            return 0;
        }

        // 3) Execute with progress
        CacheCleanResult? result = null;
        AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .Start("Moving selected caches to Trash...", _ =>
            {
                result = cleaner.MoveToTrash(selected, dryRun: false, cancellationToken);
            });

        if (result is null)
        {
            AnsiConsole.MarkupLine("[red]Unexpected error: no result.[/]");
            return 1;
        }

        PrintSummary(result);
        return result.Failed == 0 ? 0 : 1;
    }

    private static IEnumerable<CacheTarget> ApplyFilters(
        IEnumerable<CacheTarget> targets,
        long minBytes,
        bool showZero,
        string? filter)
    {
        var f = string.IsNullOrWhiteSpace(filter) ? null : filter.Trim();

        foreach (var t in targets)
        {
            var size = t.SizeBytes ?? 0;

            if (!showZero && size == 0)
                continue;

            if (size < minBytes)
                continue;

            if (f is not null)
            {
                if (!t.DisplayName.Contains(f, StringComparison.OrdinalIgnoreCase) &&
                    !t.Path.Contains(f, StringComparison.OrdinalIgnoreCase))
                    continue;
            }

            yield return t;
        }
    }

    private static List<CacheTarget> MultiSelect(List<CacheTarget> targets, bool includeContainers)
    {
        var user = targets.Where(t => !t.IsAdvanced).ToList();
        var adv = targets.Where(t => t.IsAdvanced).ToList();

        var prompt = new MultiSelectionPrompt<CacheTarget>()
            .Title("Select cache targets to move to Trash:")
            .NotRequired()
            .PageSize(20)
            .MoreChoicesText("[grey](Move up and down to reveal more caches)[/]")
            .InstructionsText("[grey](Space to toggle, Enter to confirm)[/]")
            .UseConverter(t =>
            {
                var size = t.SizeBytes.HasValue ? FormatBytes(t.SizeBytes.Value) : "-";
                var advTag = t.IsAdvanced ? " [grey](adv)[/]" : "";
                var appleTag = t.IsApple ? " [yellow](Apple)[/]" : "";
                return $"{Markup.Escape(t.DisplayName)} [grey]{Markup.Escape(size)}[/]{advTag}{appleTag}";
            });

        if (user.Count > 0)
        {
            prompt.AddChoiceGroup(
                new CacheTarget(
                    "User caches",
                    string.Empty,
                    null,
                    CacheTargetKind.UserCachesRootChild,
                    false,
                    false),
                user
            );
        }

        if (includeContainers && adv.Count > 0)
        {
            prompt.AddChoiceGroup(
                new CacheTarget(
                    "Container caches (advanced)",
                    string.Empty,
                    null,
                    CacheTargetKind.ContainerCaches,
                    true,
                    false),
                adv
            );
        }

        return AnsiConsole.Prompt(prompt);
    }

    private static List<CacheTarget> PromptPerItem(List<CacheTarget> targets)
    {
        var selected = new List<CacheTarget>();

        AnsiConsole.MarkupLine("[grey]Answer yes/no for each cache. Tip: use Ctrl+C to cancel.[/]\n");

        foreach (var t in targets)
        {
            var size = t.SizeBytes.HasValue ? FormatBytes(t.SizeBytes.Value) : "-";
            var adv = t.IsAdvanced ? " [yellow](adv)[/]" : "";
            var question = $"Move [bold]{Markup.Escape(t.DisplayName)}[/] [grey]{Markup.Escape(size)}[/]{adv} to Trash?";

            if (AnsiConsole.Confirm(question, defaultValue: false))
                selected.Add(t);
        }

        return selected;
    }

    private static long ParseSizeOrDefault(string? text, long defaultBytes)
    {
        if (string.IsNullOrWhiteSpace(text))
            return defaultBytes;

        var s = text.Trim().ToUpperInvariant().Replace(" ", "");

        long multiplier =
            s.EndsWith("TB") ? 1024L * 1024 * 1024 * 1024 :
            s.EndsWith("GB") ? 1024L * 1024 * 1024 :
            s.EndsWith("MB") ? 1024L * 1024 :
            s.EndsWith("KB") ? 1024L :
            s.EndsWith("B") ? 1L : 0;

        string numberPart;
        if (s.EndsWith("TB") || s.EndsWith("GB") || s.EndsWith("MB") || s.EndsWith("KB"))
            numberPart = s[..^2];
        else if (s.EndsWith("B"))
            numberPart = s[..^1];
        else
            throw new ArgumentException($"Unrecognized size '{text}'. Use KB, MB, GB, or TB.");

        if (!double.TryParse(numberPart, out var value) || value < 0)
            throw new ArgumentException($"Invalid size '{text}'.");

        return (long)(value * multiplier);
    }

    private static string FormatBytes(long bytes)
    {
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

    private static void PrintSummary(CacheCleanResult result)
    {
        var summary = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Requested")
            .AddColumn("Trashed")
            .AddColumn("Failed");

        summary.AddRow(result.Requested.ToString(), result.Trashed.ToString(), result.Failed.ToString());
        AnsiConsole.Write(summary);

        if (result.Failed > 0)
        {
            AnsiConsole.MarkupLine("[yellow]Some items could not be moved to Trash (in use, permission issue, or locked).[/]");
            foreach (var p in result.FailedPaths.Take(10))
                AnsiConsole.MarkupLine($"  [red]-[/] {Markup.Escape(p)}");
            if (result.FailedPaths.Count > 10)
                AnsiConsole.MarkupLine($"  [grey]...and {result.FailedPaths.Count - 10} more[/]");
        }
    }
}
