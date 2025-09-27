// ProjectContextGenerator.Console/Program.cs
using System;
using System.IO;
using ProjectContextGenerator.Domain.Config;
using ProjectContextGenerator.Domain.Options;
using ProjectContextGenerator.Domain.Rendering;
using ProjectContextGenerator.Domain.Services;
using ProjectContextGenerator.Domain.Models;
using ProjectContextGenerator.Domain.Abstractions;
using ProjectContextGenerator.Infrastructure.Config;
using ProjectContextGenerator.Infrastructure.FileSystem;
using ProjectContextGenerator.Infrastructure.Filtering;
using ProjectContextGenerator.Infrastructure.GitIgnore;
using ProjectContextGenerator.Infrastructure.Globbing;
using ProjectContextGenerator.Infrastructure.History;
using ProjectContextGenerator.Infrastructure.Process;
using System.Text;

class Program
{
    static int Main(string[] args)
    {
        string? configPath = null;
        string? profile = null;
        string? rootOverride = null;

        // Very minimal args parsing
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--config":
                    if (i + 1 < args.Length) configPath = args[++i];
                    break;
                case "--profile":
                    if (i + 1 < args.Length) profile = args[++i];
                    break;
                case "--root":
                    if (i + 1 < args.Length) rootOverride = args[++i];
                    break;
            }
        }

        TreeConfigDto? dto = null;
        string configDir = Environment.CurrentDirectory;

        // Fallback: ./.treegen.json if --config not provided
        if (string.IsNullOrWhiteSpace(configPath))
        {
            var fallback = Path.Combine(Environment.CurrentDirectory, ".treegen.json");
            if (File.Exists(fallback))
            {
                try
                {
                    (dto, configDir) = JsonFileConfigLoader.Load(fallback);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Failed to load fallback config '.treegen.json': {ex.Message}");
                    return 2;
                }
            }
        }
        else
        {
            try
            {
                (dto, configDir) = JsonFileConfigLoader.Load(configPath!);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to load config '{configPath}': {ex.Message}");
                return 2;
            }
        }

        // If no config at all, proceed with defaults
        TreeScanOptions options;
        HistoryOptions history;
        string rootPath;

        if (dto is null)
        {
            // Defaults only
            options = new TreeScanOptions();
            // Defaults for history (same as mapper defaults)
            history = new HistoryOptions(Last: 20, MaxBodyLines: 6, Detail: HistoryDetail.TitlesOnly, IncludeMerges: false);
            rootPath = string.IsNullOrWhiteSpace(rootOverride)
                ? Environment.CurrentDirectory
                : Path.GetFullPath(rootOverride, Environment.CurrentDirectory);
        }
        else
        {
            // Map config -> options + resolved root (with CLI override)
            var (mapped, historyOptions, resolvedRoot, diagnostics) =
                TreeConfigMapper.Map(dto, profile, configDir, rootOverride);

            options = mapped;
            history = historyOptions;
            rootPath = resolvedRoot;

            foreach (var d in diagnostics)
                Console.Error.WriteLine($"[warn] {d}");
        }

        // Build pipeline (same as your current sample)
        var fs = new SystemIOFileSystem();

        // Build globs based on options
        var includeMatcher = new GlobPathMatcher(options.IncludeGlobs, excludeGlobs: null);
        var excludeMatcher = (options.ExcludeGlobs is { Count: > 0 })
            ? new GlobPathMatcher(includeGlobs: ["**/*"], excludeGlobs: options.ExcludeGlobs)
            : null;

        // GitIgnore rules
        var ignoreRuleSet = options.GitIgnore == GitIgnoreMode.None
            ? EmptyIgnoreRuleSet.Instance
            : new GitIgnoreRuleProvider(fs).Load(
                rootPath,
                new IgnoreLoadingOptions(options.GitIgnore, options.GitIgnoreFileName ?? ".gitignore")
              );

        var filter = new CompositePathFilter(includeMatcher, excludeMatcher, ignoreRuleSet);

        var builder = new TreeBuilder(fs, filter);
        var renderer = new MarkdownTreeRenderer();

        var tree = builder.Build(rootPath, options);
        //Console.WriteLine(renderer.Render(tree));

        var treeOutput = renderer.Render(tree);

        // ===== Append Recent Changes AFTER the structure (composition) =====
        string historyBlock = string.Empty;
        if (history.Last > 0)
        {
            IProcessRunner runner = new SystemProcessRunner();
            IHistoryProvider historyProvider = new GitHistoryProvider(runner);
            var commits = historyProvider.GetRecentCommits(history, rootPath);

            //IHistoryRenderer historyRenderer = (renderer is PlainTextTreeRenderer)
            //    ? new PlainTextHistoryRenderer()
            //    : new MarkdownHistoryRenderer();

            IHistoryRenderer historyRenderer = new MarkdownHistoryRenderer();

            var renderedHistory = historyRenderer.Render(commits, history);
            if (!string.IsNullOrWhiteSpace(renderedHistory))
            {
                historyBlock = Environment.NewLine + Environment.NewLine + renderedHistory;
            }
        }

        Console.WriteLine(treeOutput + historyBlock);

        return 0;
    }
}