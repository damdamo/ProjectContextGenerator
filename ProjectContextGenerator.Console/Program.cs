// ProjectContextGenerator.Console/Program.cs
using System;
using System.IO;
using ProjectContextGenerator.Domain.Config;
using ProjectContextGenerator.Domain.Options;
using ProjectContextGenerator.Domain.Rendering;
using ProjectContextGenerator.Domain.Services;
using ProjectContextGenerator.Infrastructure.Config;
using ProjectContextGenerator.Infrastructure.FileSystem;
using ProjectContextGenerator.Infrastructure.Filtering;
using ProjectContextGenerator.Infrastructure.GitIgnore;
using ProjectContextGenerator.Infrastructure.Globbing;

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
        string rootPath;

        if (dto is null)
        {
            // Defaults only
            options = new TreeScanOptions();
            rootPath = string.IsNullOrWhiteSpace(rootOverride)
                ? Environment.CurrentDirectory
                : Path.GetFullPath(rootOverride, Environment.CurrentDirectory);
        }
        else
        {
            // Map config -> options + resolved root (with CLI override)
            var (mapped, resolvedRoot, diagnostics) =
                TreeConfigMapper.Map(dto, profile, configDir, rootOverride);

            options = mapped;
            rootPath = resolvedRoot;

            foreach (var d in diagnostics)
                Console.Error.WriteLine($"[warn] {d}");
        }

        // Build pipeline (same as your current sample)
        var fs = new SystemIOFileSystem();

        // Build globs based on options
        var includeMatcher = new GlobPathMatcher(options.IncludeGlobs, excludeGlobs: null);
        var excludeMatcher = (options.ExcludeGlobs is { Count: > 0 })
            ? new GlobPathMatcher(includeGlobs: new[] { "**/*" }, excludeGlobs: options.ExcludeGlobs)
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
        Console.WriteLine(renderer.Render(tree));

        return 0;
    }
}