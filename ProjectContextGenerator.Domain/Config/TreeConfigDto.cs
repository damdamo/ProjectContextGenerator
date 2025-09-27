namespace ProjectContextGenerator.Domain.Config
{
    /// <summary>
    /// Represents the raw configuration options loaded from JSON before normalization.
    /// All properties are nullable, allowing omission and fallback to defaults.
    /// This DTO is later mapped to <see cref="Options.TreeScanOptions"/> via <see cref="TreeConfigMapper"/>.
    /// </summary>
    public sealed class TreeConfigDto
    {
        /// <summary>
        /// Version of the config schema. Currently only <c>1</c> is supported.
        /// Unknown versions are reported as diagnostics.
        /// </summary>
        public int? Version { get; init; }

        /// <summary>
        /// Root directory to scan. Can be relative (to the config file’s directory) or absolute.
        /// If null, defaults to the current working directory unless overridden by CLI.
        /// </summary>
        public string? Root { get; init; }

        /// <summary>
        /// Maximum recursion depth when scanning.
        /// <c>0</c> = root only, <c>1</c> = root + direct children, <c>-1</c> = unlimited.
        /// </summary>
        public int? MaxDepth { get; init; }

        /// <summary>
        /// Glob patterns specifying which files or directories to include.
        /// If null, everything is included by default.
        /// Examples: <c>["*.cs", "*.csproj"]</c>.
        /// </summary>
        public IReadOnlyList<string>? Include { get; init; }

        /// <summary>
        /// Glob patterns specifying which files or directories to exclude.
        /// Examples: <c>["bin/", "obj/", ".git/"]</c>.
        /// </summary>
        public IReadOnlyList<string>? Exclude { get; init; }

        /// <summary>
        /// Mode controlling how <c>.gitignore</c> files are applied.
        /// Valid values: <c>"None"</c>, <c>"RootOnly"</c>, <c>"Nested"</c>.
        /// Defaults to RootOnly when unspecified or invalid.
        /// </summary>
        public string? GitIgnore { get; init; }

        /// <summary>
        /// Name of the ignore file to load rules from. Typically <c>".gitignore"</c>.
        /// Allows custom ignore files for testing or non-Git scenarios.
        /// </summary>
        public string? GitIgnoreFileName { get; init; }

        /// <summary>
        /// If true, directories are listed before files within each folder.
        /// Default is true.
        /// </summary>
        public bool? SortDirectoriesFirst { get; init; }

        /// <summary>
        /// If true, collapses single-child directory chains into a combined path.
        /// Improves readability for deeply nested structures.
        /// Default is true.
        /// </summary>
        public bool? CollapseSingleChildDirectories { get; init; }

        /// <summary>
        /// Maximum number of items to display per directory.
        /// When set, excess entries are replaced with a placeholder node (e.g. "… (+N more)").
        /// Null means unlimited.
        /// </summary>
        public int? MaxItemsPerDirectory { get; init; }

        /// <summary>
        /// If true, only directories are rendered; files are skipped.
        /// Useful to produce a skeleton view of folder hierarchy.
        /// Filtering rules (include/exclude/gitignore) still apply to directories.
        /// </summary>
        public bool? DirectoriesOnly { get; init; }

        /// <summary>
        /// Profiles allow defining named presets. Each profile is a partial configuration
        /// that overrides values from the root configuration when selected via CLI.
        /// Example: "fast", "full", "csharp".
        /// </summary>
        public IReadOnlyDictionary<string, TreeConfigDto>? Profiles { get; init; }
    }
}