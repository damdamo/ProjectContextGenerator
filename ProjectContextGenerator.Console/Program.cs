using ProjectContextGenerator.Domain.Config;
using ProjectContextGenerator.Domain.Options;
using ProjectContextGenerator.Domain.Rendering;
using ProjectContextGenerator.Domain.Services;
using ProjectContextGenerator.Domain.Abstractions;
using ProjectContextGenerator.Infrastructure.Config;
using ProjectContextGenerator.Infrastructure.FileSystem;
using ProjectContextGenerator.Infrastructure.Filtering;
using ProjectContextGenerator.Infrastructure.GitIgnore;
using ProjectContextGenerator.Infrastructure.Globbing;
using ProjectContextGenerator.Infrastructure.History;
using ProjectContextGenerator.Infrastructure.Process;

class Program
{
    /// <summary>
    /// Entry point for the console sample. Loads configuration (with optional profile),
    /// maps it to runtime options via ContextConfigMapper (even when no config file is present),
    /// builds the directory tree using filtering rules, renders it to Markdown,
    /// and optionally appends a block of recent Git history.
    /// </summary>
    static int Main(string[] args)
    {
        string? configPath = null;
        string? profile = null;
        string? rootOverride = null;

        // Minimal CLI parsing
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

        ContextConfigDto? dto = null;
        string configDir = Environment.CurrentDirectory;

        // Fallback to ./.contextgen.json if --config is not supplied
        if (string.IsNullOrWhiteSpace(configPath))
        {
            var fallback = Path.Combine(Environment.CurrentDirectory, ".contextgen.json");
            if (File.Exists(fallback))
            {
                try { (dto, configDir) = JsonFileConfigLoader.Load(fallback); }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Failed to load fallback config '.contextgen.json': {ex.Message}");
                    return 2;
                }
            }
        }
        else
        {
            try { (dto, configDir) = JsonFileConfigLoader.Load(configPath!); }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to load config '{configPath}': {ex.Message}");
                return 2;
            }
        }

        // Map configuration to runtime options (even when dto is null)
        var configToMap = dto ?? new ContextConfigDto();
        var (scan, history, content, rootPath, diagnostics) =
            ContextConfigMapper.Map(configToMap, profile, configDir, rootOverride);

        foreach (var d in diagnostics)
            Console.Error.WriteLine($"[warn] {d}");

        // Build pipeline
        var fs = new SystemIOFileSystem();

        // Build globs based on options (include/exclude)
        var includeMatcher = new GlobPathMatcher(scan.IncludeGlobs, excludeGlobs: null);
        var excludeMatcher = (scan.ExcludeGlobs is { Count: > 0 })
            ? new GlobPathMatcher(includeGlobs: ["**/*"], excludeGlobs: scan.ExcludeGlobs)
            : null;

        // GitIgnore rules (None | RootOnly | Nested)
        var ignoreRuleSet = scan.GitIgnore == GitIgnoreMode.None
            ? EmptyIgnoreRuleSet.Instance
            : new GitIgnoreRuleProvider(fs).Load(
                rootPath,
                new IgnoreLoadingOptions(scan.GitIgnore, scan.GitIgnoreFileName ?? ".gitignore")
              );

        var filter = new CompositePathFilter(includeMatcher, excludeMatcher, ignoreRuleSet);

        var builder = new TreeBuilder(fs, filter);

        // Use the content-aware Markdown renderer
        var renderer = new MarkdownTreeRenderer(fs, rootPath, content);

        // Build tree and render
        var tree = builder.Build(rootPath, scan);
        var treeOutput = renderer.Render(tree);

        // Optionally append Recent Changes after the structure
        string historyBlock = string.Empty;
        if (history.Enabled && history.Last > 0)
        {
            IProcessRunner runner = new SystemProcessRunner();
            IHistoryProvider historyProvider = new GitHistoryProvider(runner);
            var commits = historyProvider.GetRecentCommits(history, rootPath);

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