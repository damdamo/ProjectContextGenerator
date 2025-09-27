using ProjectContextGenerator.Domain.Options;

namespace ProjectContextGenerator.Domain.Config
{
    /// <summary>
    /// Maps a raw <see cref="TreeConfigDto"/> into a validated and normalized <see cref="TreeScanOptions"/>.
    /// Handles profile selection, default values, validation, glob normalization, and root resolution.
    /// </summary>
    public static class TreeConfigMapper
    {
        /// <summary>
        /// Converts a <see cref="TreeConfigDto"/> into a <see cref="TreeScanOptions"/>,
        /// returning the final resolved root and diagnostics.
        /// Priority for root resolution: CLI override > config JSON > default ".".
        /// </summary>
        /// <param name="config">Root configuration loaded from JSON.</param>
        /// <param name="profileName">Optional profile to apply (overrides root config fields).</param>
        /// <param name="configDirectory">Directory where the config file lives (used to resolve relative config root).</param>
        /// <param name="rootOverride">Optional CLI root value (if relative, resolved against Environment.CurrentDirectory).</param>
        public static (TreeScanOptions Options, string Root, IReadOnlyList<string> Diagnostics)
            Map(TreeConfigDto config, string? profileName, string configDirectory, string? rootOverride)
        {
            var diagnostics = new List<string>();

            // 1) Merge root with profile if requested
            var effectiveConfig = profileName != null
                ? MergeProfile(config, profileName, diagnostics)
                : config;

            // 2) Validate version
            if (effectiveConfig.Version is not null && effectiveConfig.Version != 1)
            {
                diagnostics.Add($"Unsupported config version '{effectiveConfig.Version}'. Expected 1.");
            }

            // 3) Validate and normalize MaxDepth
            var maxDepth = effectiveConfig.MaxDepth ?? 4;
            if (maxDepth < -1)
            {
                diagnostics.Add($"Invalid maxDepth '{maxDepth}'. Using -1 instead.");
                maxDepth = -1;
            }

            // 4) Normalize include/exclude patterns
            var include = NormalizePatterns(effectiveConfig.Include);
            var exclude = NormalizePatterns(effectiveConfig.Exclude);

            // 5) Parse gitIgnore
            var gitIgnore = ParseGitIgnore(effectiveConfig.GitIgnore, diagnostics);

            // 6) Other scalar options with defaults
            var gitIgnoreFileName = effectiveConfig.GitIgnoreFileName ?? ".gitignore";
            var sortDirectoriesFirst = effectiveConfig.SortDirectoriesFirst ?? true;
            var collapseSingleChild = effectiveConfig.CollapseSingleChildDirectories ?? true;
            var directoriesOnly = effectiveConfig.DirectoriesOnly ?? false;

            int? maxItemsPerDirectory = null;
            if (effectiveConfig.MaxItemsPerDirectory.HasValue)
            {
                var val = effectiveConfig.MaxItemsPerDirectory.Value;
                if (val < 0)
                {
                    diagnostics.Add($"Invalid maxItemsPerDirectory '{val}'. Ignoring value.");
                }
                else
                {
                    maxItemsPerDirectory = val;
                }
            }

            // 7) Resolve root with priority: CLI override > config > default "."
            var root = ResolveRoot(rootOverride, effectiveConfig.Root, configDirectory);

            // 8) Build final TreeScanOptions
            var options = new TreeScanOptions(
                MaxDepth: maxDepth,
                IncludeGlobs: include,
                ExcludeGlobs: exclude,
                SortDirectoriesFirst: sortDirectoriesFirst,
                CollapseSingleChildDirectories: collapseSingleChild,
                MaxItemsPerDirectory: maxItemsPerDirectory,
                GitIgnore: gitIgnore,
                GitIgnoreFileName: gitIgnoreFileName,
                DirectoriesOnly: directoriesOnly
            );

            return (options, root, diagnostics);
        }

        /// <summary>
        /// Applies a profile on top of the root configuration.
        /// </summary>
        private static TreeConfigDto MergeProfile(TreeConfigDto root, string profileName, List<string> diagnostics)
        {
            if (root.Profiles == null || !root.Profiles.TryGetValue(profileName, out var profile))
            {
                diagnostics.Add($"Profile '{profileName}' not found. Falling back to root configuration.");
                return root;
            }

            return new TreeConfigDto
            {
                Version = root.Version ?? profile.Version,
                Root = profile.Root ?? root.Root,
                MaxDepth = profile.MaxDepth ?? root.MaxDepth,
                Include = profile.Include ?? root.Include,
                Exclude = profile.Exclude ?? root.Exclude,
                GitIgnore = profile.GitIgnore ?? root.GitIgnore,
                GitIgnoreFileName = profile.GitIgnoreFileName ?? root.GitIgnoreFileName,
                SortDirectoriesFirst = profile.SortDirectoriesFirst ?? root.SortDirectoriesFirst,
                CollapseSingleChildDirectories = profile.CollapseSingleChildDirectories ?? root.CollapseSingleChildDirectories,
                MaxItemsPerDirectory = profile.MaxItemsPerDirectory ?? root.MaxItemsPerDirectory,
                DirectoriesOnly = profile.DirectoriesOnly ?? root.DirectoriesOnly,
                Profiles = root.Profiles // keep original profiles intact
            };
        }

        /// <summary>
        /// Expands human-friendly patterns into full glob expressions and normalizes slashes.
        /// Examples:
        ///  - "bin/"   -> "**/bin/**"
        ///  - "*.cs"   -> "**/*.cs"
        ///  - "README" -> "**/README"
        /// Already-globbed patterns are preserved.
        /// </summary>
        private static IReadOnlyList<string>? NormalizePatterns(IReadOnlyList<string>? patterns)
        {
            if (patterns == null) return null;

            var result = new List<string>();

            foreach (var p in patterns)
            {
                if (string.IsNullOrWhiteSpace(p))
                    continue;

                var normalized = p.Replace('\\', '/').Trim();

                if (normalized.EndsWith("/"))
                {
                    // Directory shorthand -> **/dir/**
                    result.Add($"**/{normalized}**");
                }
                else if (normalized.StartsWith("*."))
                {
                    // Extension shorthand -> **/*.ext
                    result.Add($"**/{normalized}");
                }
                else if (!normalized.Contains("*") && !normalized.Contains("?") && !normalized.Contains("["))
                {
                    // Plain name -> **/name
                    result.Add($"**/{normalized}");
                }
                else
                {
                    // Already a glob
                    result.Add(normalized);
                }
            }

            return result.Distinct().ToList();
        }

        /// <summary>
        /// Parses the gitIgnore string into the corresponding enum.
        /// Falls back to RootOnly with a diagnostic on unknown values.
        /// </summary>
        private static GitIgnoreMode ParseGitIgnore(string? value, List<string> diagnostics)
        {
            if (string.IsNullOrWhiteSpace(value))
                return GitIgnoreMode.RootOnly; // default

            if (Enum.TryParse<GitIgnoreMode>(value, ignoreCase: true, out var mode))
                return mode;

            diagnostics.Add($"Unknown gitIgnore value '{value}'. Falling back to RootOnly.");
            return GitIgnoreMode.RootOnly;
        }

        /// <summary>
        /// Resolves the effective root path based on priority:
        /// 1) CLI override (relative to Environment.CurrentDirectory if not rooted)
        /// 2) Config JSON root (relative to configDirectory if not rooted)
        /// 3) Default "."
        /// Always returns an absolute, normalized path.
        /// </summary>
        private static string ResolveRoot(string? rootOverride, string? configRoot, string configDirectory)
        {
            if (!string.IsNullOrWhiteSpace(rootOverride))
            {
                var candidate = rootOverride!;
                return Path.IsPathRooted(candidate)
                    ? Path.GetFullPath(candidate)
                    : Path.GetFullPath(candidate, Environment.CurrentDirectory);
            }

            var fromConfig = configRoot ?? ".";
            return Path.IsPathRooted(fromConfig)
                ? Path.GetFullPath(fromConfig)
                : Path.GetFullPath(fromConfig, configDirectory);
        }
    }
}