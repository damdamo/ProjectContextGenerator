using ProjectContextGenerator.Domain.Options;

namespace ProjectContextGenerator.Domain.Config
{
    /// <summary>
    /// Maps a raw <see cref="ContextConfigDto"/> into normalized runtime options
    /// (<see cref="TreeScanOptions"/>, <see cref="HistoryOptions"/>, <see cref="ContentOptions"/>),
    /// and resolves the effective scan root.
    /// </summary>
    public static class ContextConfigMapper
    {
        /// <summary>
        /// Converts a <see cref="ContextConfigDto"/> into runtime options, returning the resolved root and diagnostics.
        /// Root resolution priority: CLI override &gt; config JSON &gt; default ".".
        /// </summary>
        public static (TreeScanOptions Options, HistoryOptions History, ContentOptions Content, string Root, IReadOnlyList<string> Diagnostics)
            Map(ContextConfigDto config, string? profileName, string configDirectory, string? rootOverride)
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
            if (maxDepth < -1) { diagnostics.Add($"Invalid maxDepth '{maxDepth}'. Using -1."); maxDepth = -1; }

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
                if (val < 0) diagnostics.Add($"Invalid maxItemsPerDirectory '{val}'. Ignoring value.");
                else maxItemsPerDirectory = val;
            }

            // 7) Resolve root
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

            // 9) Build History + Content options
            var history = BuildHistoryOptions(effectiveConfig.History, diagnostics);
            var content = BuildContentOptions(effectiveConfig.Content, diagnostics);

            return (options, history, content, root, diagnostics);
        }

        /// <summary>
        /// Applies a profile on top of the root configuration (deep merge where relevant).
        /// </summary>
        private static ContextConfigDto MergeProfile(ContextConfigDto root, string profileName, List<string> diagnostics)
        {
            if (root.Profiles == null || !root.Profiles.TryGetValue(profileName, out var profile))
            {
                diagnostics.Add($"Profile '{profileName}' not found. Falling back to root configuration.");
                return root;
            }

            return new ContextConfigDto
            {
                Version = profile.Version ?? root.Version,
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
                Profiles = root.Profiles, // keep original profiles intact
                History = MergeHistoryDto(root.History, profile.History),
                Content = MergeContentDto(root.Content, profile.Content)
            };
        }

        /// <summary>
        /// Expands human-friendly patterns into full glob expressions and normalizes slashes.
        /// </summary>
        private static IReadOnlyList<string>? NormalizePatterns(IReadOnlyList<string>? patterns)
        {
            if (patterns == null) return null;

            var result = new List<string>();

            foreach (var p in patterns)
            {
                if (string.IsNullOrWhiteSpace(p)) continue;

                var s = p.Replace('\\', '/').Trim();

                if (s.EndsWith('/')) result.Add($"**/{s}**");   // "obj/" -> "**/obj/**"
                else if (s.StartsWith("*.")) result.Add($"**/{s}");     // "*.cs" -> "**/*.cs"
                else if (!s.Contains('*') && !s.Contains('?') && !s.Contains('['))
                    result.Add($"**/{s}");       // "README" -> "**/README"
                else result.Add(s);            // already globbed
            }

            return [.. result.Distinct()];
        }

        /// <summary>
        /// Parses the gitIgnore string into the corresponding enum with RootOnly default.
        /// </summary>
        private static GitIgnoreMode ParseGitIgnore(string? value, List<string> diagnostics)
        {
            if (string.IsNullOrWhiteSpace(value))
                return GitIgnoreMode.RootOnly;

            if (Enum.TryParse<GitIgnoreMode>(value, ignoreCase: true, out var mode))
                return mode;

            diagnostics.Add($"Unknown gitIgnore value '{value}'. Falling back to RootOnly.");
            return GitIgnoreMode.RootOnly;
        }

        /// <summary>
        /// Resolves the effective root path (absolute) based on priority:
        /// 1) CLI override (relative to Environment.CurrentDirectory if not rooted)
        /// 2) Config JSON root (relative to configDirectory if not rooted)
        /// 3) Default "."
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

        /// <summary>
        /// Builds normalized history options from the DTO with defaults and diagnostics.
        /// </summary>
        private static HistoryOptions BuildHistoryOptions(HistoryDto? dto, List<string> diagnostics)
        {
            // Enabled defaults to true for backward compatibility.
            var enabled = dto?.Enabled ?? true;

            var last = dto?.Last ?? 20;
            if (last < 0) { diagnostics.Add($"Invalid history.last '{last}'. Using 0."); last = 0; }

            var maxBody = dto?.MaxBodyLines ?? 6;
            if (maxBody < 0) { diagnostics.Add($"Invalid history.maxBodyLines '{maxBody}'. Using 0."); maxBody = 0; }

            var detailStr = dto?.Detail ?? "TitlesOnly";
            if (!Enum.TryParse<HistoryDetail>(detailStr, ignoreCase: true, out HistoryDetail detail))
            {
                diagnostics.Add($"Unknown history.detail '{detailStr}'. Falling back to TitlesOnly.");
                detail = HistoryDetail.TitlesOnly;
            }

            var includeMerges = dto?.IncludeMerges ?? false;

            return new HistoryOptions(
                Enabled: enabled,
                Last: last,
                MaxBodyLines: maxBody,
                Detail: detail,
                IncludeMerges: includeMerges
            );
        }

        /// <summary>
        /// Builds normalized content options from the DTO with defaults and diagnostics.
        /// </summary>
        private static ContentOptions BuildContentOptions(ContentDto? dto, List<string> diagnostics)
        {
            if (dto is null) return new ContentOptions(); // defaults (Enabled=false)

            bool enabled = dto.Enabled ?? false;

            int indentDepth = dto.IndentDepth ?? 1;
            if (indentDepth < -1) { diagnostics.Add($"Invalid content.indentDepth '{indentDepth}'. Using -1."); indentDepth = -1; }

            int tabWidth = dto.TabWidth ?? 4;
            if (tabWidth != 2 && tabWidth != 4 && tabWidth != 8)
            {
                diagnostics.Add($"Suspicious content.tabWidth '{tabWidth}'. Using 4.");
                tabWidth = 4;
            }

            bool detect = dto.DetectTabWidth ?? true;

            int maxLines = dto.MaxLinesPerFile ?? 300;
            if (maxLines < -1 || maxLines == 0)
            {
                diagnostics.Add($"Invalid content.maxLinesPerFile '{maxLines}'. Using 300.");
                maxLines = 300;
            }

            bool showLineNumbers = dto.ShowLineNumbers ?? false;

            int contextPadding = dto.ContextPadding ?? 1;
            if (contextPadding < 0) { diagnostics.Add($"Invalid content.contextPadding '{contextPadding}'. Using 0."); contextPadding = 0; }

            int? maxFiles = dto.MaxFiles;
            if (maxFiles is int mf && mf < 0) { diagnostics.Add($"Invalid content.maxFiles '{mf}'. Ignoring."); maxFiles = null; }

            return new ContentOptions(enabled, indentDepth, tabWidth, detect, maxLines, showLineNumbers, contextPadding, maxFiles);
        }

        private static HistoryDto? MergeHistoryDto(HistoryDto? root, HistoryDto? profile)
        {
            if (root is null && profile is null) return null;
            if (root is null) return profile;
            if (profile is null) return root;

            return new HistoryDto
            {
                Enabled = profile.Enabled ?? root.Enabled,
                Last = profile.Last ?? root.Last,
                MaxBodyLines = profile.MaxBodyLines ?? root.MaxBodyLines,
                Detail = profile.Detail ?? root.Detail,
                IncludeMerges = profile.IncludeMerges ?? root.IncludeMerges
            };
        }

        private static ContentDto? MergeContentDto(ContentDto? root, ContentDto? profile)
        {
            if (root is null && profile is null) return null;
            if (root is null) return profile;
            if (profile is null) return root;

            return new ContentDto
            {
                Enabled = profile.Enabled ?? root.Enabled,
                IndentDepth = profile.IndentDepth ?? root.IndentDepth,
                TabWidth = profile.TabWidth ?? root.TabWidth,
                DetectTabWidth = profile.DetectTabWidth ?? root.DetectTabWidth,
                MaxLinesPerFile = profile.MaxLinesPerFile ?? root.MaxLinesPerFile,
                ShowLineNumbers = profile.ShowLineNumbers ?? root.ShowLineNumbers,
                ContextPadding = profile.ContextPadding ?? root.ContextPadding,
                MaxFiles = profile.MaxFiles ?? root.MaxFiles
            };
        }
    }
}