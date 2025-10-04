using ProjectContextGenerator.Domain.Options;

namespace ProjectContextGenerator.Domain.Config
{
    /// <summary>
    /// Maps a raw <see cref="ContextConfigDto"/> into normalized runtime options
    /// (<see cref="TreeScanOptions"/>, <see cref="HistoryOptions"/>, <see cref="ContentOptions"/>),
    /// and resolves the effective scan root.
    ///
    /// Notes:
    /// - Option record defaults are runtime safety baselines.
    /// - Policy defaults (when config is missing/partial) are centralized below in <see cref="Defaults"/>.
    ///   The mapper applies these consistently and emits diagnostics when normalizing suspicious values.
    /// </summary>
    public static class ContextConfigMapper
    {
        /// <summary>
        /// Policy defaults and allowed values for config normalization.
        /// Kept local to the mapper to avoid leaking policy outside this boundary.
        /// </summary>
        private static class Defaults
        {
            // Tree
            public static readonly int DefaultMaxDepth = 4;
            public static readonly GitIgnoreMode DefaultGitIgnoreMode = GitIgnoreMode.Nested;
            public static readonly string DefaultGitIgnoreFileName = ".gitignore";
            public static readonly bool DefaultSortDirectoriesFirst = true;
            public static readonly bool DefaultCollapseSingleChildDirectories = true;
            public static readonly bool DefaultDirectoriesOnly = false;
            public static readonly int? DefaultMaxItemsPerDirectory = null;

            // When the config file is missing/empty, apply these safe excludes by default
            public static readonly IReadOnlyList<string> EmptyConfigExcludeGlobs =
            [
                "bin/", "obj/", ".git/", "node_modules/"
            ];

            // History (policy)
            public static readonly bool DefaultHistoryEnabled = false;
            public static readonly int DefaultHistoryLast = 10;     // per user request
            public static readonly int DefaultHistoryMaxBodyLines = 6;
            public static readonly HistoryDetail DefaultHistoryDetail = HistoryDetail.TitlesOnly;
            public static readonly bool DefaultHistoryIncludeMerges = false;

            // Content (policy)
            public static readonly bool DefaultContentEnabled = false;
            public static readonly int DefaultIndentDepth = 1;      // -1 means keep all
            public static readonly int DefaultTabWidth = 4;
            public static readonly bool DefaultDetectTabWidth = true;
            public static readonly int DefaultMaxLinesPerFile = 300; // -1 means unlimited
            public static readonly bool DefaultShowLineNumbers = false;
            public static readonly int DefaultContextPadding = 1;
            public static readonly int? DefaultMaxFiles = null;

            // Allowed sets
            public static readonly HashSet<int> AllowedTabWidths = new([2, 4, 8]);

            // Diagnostics templates (compose messages from the same source-of-truth numbers)
            public const string DiagInvalidMaxDepth = "Invalid maxDepth '{0}'. Using -1.";
            public const string DiagUnknownGitIgnore = "Unknown gitIgnore value '{0}'. Falling back to {1}.";
            public const string DiagInvalidMaxItemsPerDir = "Invalid maxItemsPerDirectory '{0}'. Ignoring value.";

            public const string DiagInvalidIndentDepth = "Invalid content.indentDepth '{0}'. Using -1.";
            public const string DiagSuspiciousTabWidth = "Suspicious content.tabWidth '{0}'. Using {1}.";
            public const string DiagInvalidMaxLines = "Invalid content.maxLinesPerFile '{0}'. Using {1}.";
            public const string DiagInvalidContextPadding = "Invalid content.contextPadding '{0}'. Using 0.";
            public const string DiagInvalidMaxFiles = "Invalid content.maxFiles '{0}'. Ignoring.";

            public const string DiagInvalidHistoryLast = "Invalid history.last '{0}'. Using 0.";
            public const string DiagInvalidHistoryMaxBody = "Invalid history.maxBodyLines '{0}'. Using 0.";
            public const string DiagUnknownHistoryDetail = "Unknown history.detail '{0}'. Falling back to TitlesOnly.";
        }

        /// <summary>
        /// Converts a <see cref="ContextConfigDto"/> into runtime options, returning the resolved root and diagnostics.
        /// Root resolution priority: CLI override &gt; config JSON &gt; default "." (config directory).
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

            // Determine if the config is effectively "empty" (used for default excludes)
            bool isConfigEmpty = IsEffectivelyEmpty(effectiveConfig);

            // 3) Validate and normalize MaxDepth
            var maxDepth = effectiveConfig.MaxDepth ?? Defaults.DefaultMaxDepth;
            if (maxDepth < -1)
            {
                diagnostics.Add(string.Format(Defaults.DiagInvalidMaxDepth, maxDepth));
                maxDepth = -1;
            }

            // 4) Normalize include/exclude patterns (and inject safe excludes when config is empty)
            var include = NormalizePatterns(effectiveConfig.Include);
            var exclude = NormalizePatterns(effectiveConfig.Exclude);
            if (exclude is null && isConfigEmpty)
            {
                exclude = NormalizePatterns(Defaults.EmptyConfigExcludeGlobs);
            }

            // 5) Parse gitIgnore
            var gitIgnore = ParseGitIgnore(effectiveConfig.GitIgnore, diagnostics);

            // 6) Other scalar options with defaults
            var gitIgnoreFileName = effectiveConfig.GitIgnoreFileName ?? Defaults.DefaultGitIgnoreFileName;
            var sortDirectoriesFirst = effectiveConfig.SortDirectoriesFirst ?? Defaults.DefaultSortDirectoriesFirst;
            var collapseSingleChild = effectiveConfig.CollapseSingleChildDirectories ?? Defaults.DefaultCollapseSingleChildDirectories;
            var directoriesOnly = effectiveConfig.DirectoriesOnly ?? Defaults.DefaultDirectoriesOnly;

            int? maxItemsPerDirectory = Defaults.DefaultMaxItemsPerDirectory;
            if (effectiveConfig.MaxItemsPerDirectory.HasValue)
            {
                var val = effectiveConfig.MaxItemsPerDirectory.Value;
                if (val < 0) diagnostics.Add(string.Format(Defaults.DiagInvalidMaxItemsPerDir, val));
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
        /// Parses the gitIgnore string into the corresponding enum with Nested default.
        /// </summary>
        private static GitIgnoreMode ParseGitIgnore(string? value, List<string> diagnostics)
        {
            if (string.IsNullOrWhiteSpace(value))
                return Defaults.DefaultGitIgnoreMode;

            if (Enum.TryParse<GitIgnoreMode>(value, ignoreCase: true, out var mode))
                return mode;

            diagnostics.Add(string.Format(Defaults.DiagUnknownGitIgnore, value, Defaults.DefaultGitIgnoreMode));
            return Defaults.DefaultGitIgnoreMode;
        }

        /// <summary>
        /// Resolves the effective root path (absolute) based on priority:
        /// 1) CLI override (relative to Environment.CurrentDirectory if not rooted)
        /// 2) Config JSON root (relative to configDirectory if not rooted)
        /// 3) Default "." (relative to configDirectory)
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
            var enabled = dto?.Enabled ?? Defaults.DefaultHistoryEnabled;

            var last = dto?.Last ?? Defaults.DefaultHistoryLast;
            if (last < 0)
            {
                diagnostics.Add(string.Format(Defaults.DiagInvalidHistoryLast, last));
                last = 0;
            }

            var maxBody = dto?.MaxBodyLines ?? Defaults.DefaultHistoryMaxBodyLines;
            if (maxBody < 0)
            {
                diagnostics.Add(string.Format(Defaults.DiagInvalidHistoryMaxBody, maxBody));
                maxBody = 0;
            }

            var detailStr = dto?.Detail ?? Defaults.DefaultHistoryDetail.ToString();
            if (!Enum.TryParse<HistoryDetail>(detailStr, ignoreCase: true, out HistoryDetail detail))
            {
                diagnostics.Add(string.Format(Defaults.DiagUnknownHistoryDetail, detailStr));
                detail = Defaults.DefaultHistoryDetail;
            }

            var includeMerges = dto?.IncludeMerges ?? Defaults.DefaultHistoryIncludeMerges;

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
            if (dto is null)
            {
                // Keep record defaults (Enabled=false) when no "content" block is present.
                return new ContentOptions();
            }

            bool enabled = dto.Enabled ?? Defaults.DefaultContentEnabled;

            int indentDepth = NormalizeIndentDepth(dto.IndentDepth, diagnostics);
            int tabWidth = NormalizeTabWidth(dto.TabWidth, diagnostics);
            bool detect = dto.DetectTabWidth ?? Defaults.DefaultDetectTabWidth;

            int maxLines = NormalizeMaxLinesPerFile(dto.MaxLinesPerFile, diagnostics);
            bool showLineNumbers = dto.ShowLineNumbers ?? Defaults.DefaultShowLineNumbers;

            int contextPadding = NormalizeContextPadding(dto.ContextPadding, diagnostics);
            int? maxFiles = NormalizeMaxFiles(dto.MaxFiles, diagnostics);

            var include = NormalizePatterns(dto.Include);

            return new ContentOptions(
                Enabled: enabled,
                IndentDepth: indentDepth,
                TabWidth: tabWidth,
                DetectTabWidth: detect,
                MaxLinesPerFile: maxLines,
                ShowLineNumbers: showLineNumbers,
                ContextPadding: contextPadding,
                MaxFiles: maxFiles,
                Include: include
            );
        }

        private static int NormalizeIndentDepth(int? value, List<string> diagnostics)
        {
            if (!value.HasValue) return Defaults.DefaultIndentDepth;
            var v = value.Value;
            if (v < -1)
            {
                diagnostics.Add(string.Format(Defaults.DiagInvalidIndentDepth, v));
                return -1;
            }
            return v;
        }

        private static int NormalizeTabWidth(int? value, List<string> diagnostics)
        {
            var v = value ?? Defaults.DefaultTabWidth;
            if (!Defaults.AllowedTabWidths.Contains(v))
            {
                diagnostics.Add(string.Format(Defaults.DiagSuspiciousTabWidth, v, Defaults.DefaultTabWidth));
                return Defaults.DefaultTabWidth;
            }
            return v;
        }

        private static int NormalizeMaxLinesPerFile(int? value, List<string> diagnostics)
        {
            var v = value ?? Defaults.DefaultMaxLinesPerFile;
            if (v < -1 || v == 0)
            {
                diagnostics.Add(string.Format(Defaults.DiagInvalidMaxLines, v, Defaults.DefaultMaxLinesPerFile));
                return Defaults.DefaultMaxLinesPerFile;
            }
            return v;
        }

        private static int NormalizeContextPadding(int? value, List<string> diagnostics)
        {
            var v = value ?? Defaults.DefaultContextPadding;
            if (v < 0)
            {
                diagnostics.Add(string.Format(Defaults.DiagInvalidContextPadding, v));
                return 0;
            }
            return v;
        }

        private static int? NormalizeMaxFiles(int? value, List<string> diagnostics)
        {
            if (!value.HasValue) return Defaults.DefaultMaxFiles;
            var v = value.Value;
            if (v < 0)
            {
                diagnostics.Add(string.Format(Defaults.DiagInvalidMaxFiles, v));
                return null;
            }
            return v;
        }

        /// <summary>
        /// Determines if a config object is effectively "empty" (i.e., no meaningful settings provided).
        /// Used to decide whether to inject safe default excludes when no config is present.
        /// </summary>
        private static bool IsEffectivelyEmpty(ContextConfigDto c)
        {
            bool rootEmpty =
                c.Root is null &&
                c.MaxDepth is null &&
                c.Include is null &&
                c.Exclude is null &&
                c.GitIgnore is null &&
                c.GitIgnoreFileName is null &&
                c.SortDirectoriesFirst is null &&
                c.CollapseSingleChildDirectories is null &&
                c.MaxItemsPerDirectory is null &&
                c.DirectoriesOnly is null &&
                c.Profiles is null;

            bool historyEmpty = c.History is null ||
                                (c.History.Enabled is null &&
                                 c.History.Last is null &&
                                 c.History.MaxBodyLines is null &&
                                 c.History.Detail is null &&
                                 c.History.IncludeMerges is null);

            bool contentEmpty = c.Content is null ||
                                (c.Content.Enabled is null &&
                                 c.Content.IndentDepth is null &&
                                 c.Content.TabWidth is null &&
                                 c.Content.DetectTabWidth is null &&
                                 c.Content.MaxLinesPerFile is null &&
                                 c.Content.ShowLineNumbers is null &&
                                 c.Content.ContextPadding is null &&
                                 c.Content.MaxFiles is null &&
                                 c.Content.Include is null);

            return rootEmpty && historyEmpty && contentEmpty;
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
                MaxFiles = profile.MaxFiles ?? root.MaxFiles,
                Include = profile.Include ?? root.Include
            };
        }
    }
}