namespace ProjectContextGenerator.Domain.Config
{
    /// <summary>
    /// Configuration block for recent Git history rendering.
    /// </summary>
    public sealed class HistoryDto
    {
        /// <summary>Number of latest commits to include (default 20).</summary>
        public int? Last { get; init; }

        /// <summary>Maximum number of body lines to keep (default 6).</summary>
        public int? MaxBodyLines { get; init; }

        /// <summary>"TitlesOnly" | "TitleAndBody" (default "TitlesOnly").</summary>
        public string? Detail { get; init; }

        /// <summary>If true, include merge commits (default false).</summary>
        public bool? IncludeMerges { get; init; }
    }

    /// <summary>
    /// Configuration block for file content rendering under file nodes.
    /// All properties are optional and fall back to sensible defaults in the mapper.
    /// </summary>
    public sealed class ContentDto
    {
        /// <summary>When true, file contents are rendered beneath file nodes.</summary>
        public bool? Enabled { get; init; }

        /// <summary>Maximum indentation depth to keep; -1 keeps all depths.</summary>
        public int? IndentDepth { get; init; }

        /// <summary>Tab width for tabs-to-spaces expansion (e.g., 2, 4, 8).</summary>
        public int? TabWidth { get; init; }

        /// <summary>When true, attempts to auto-detect tab width on the first lines.</summary>
        public bool? DetectTabWidth { get; init; }

        /// <summary>Maximum number of lines to render per file; -1 means unlimited.</summary>
        public int? MaxLinesPerFile { get; init; }

        /// <summary>When true, shows line numbers next to each rendered line.</summary>
        public bool? ShowLineNumbers { get; init; }

        /// <summary>Number of context lines to retain around kept lines.</summary>
        public int? ContextPadding { get; init; }

        /// <summary>Optional global cap on the number of files with rendered content.</summary>
        public int? MaxFiles { get; init; }
    }

    /// <summary>
    /// Represents the raw configuration options loaded from JSON before normalization.
    /// All properties are nullable, allowing omission and fallback to defaults.
    /// This DTO is later mapped to runtime options via <see cref="ContextConfigMapper"/>.
    /// </summary>
    public sealed class ContextConfigDto
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

        /// <summary>If true, directories are listed before files. Default is true.</summary>
        public bool? SortDirectoriesFirst { get; init; }

        /// <summary>
        /// If true, collapses single-child directory chains into a combined path for readability.
        /// Default is true.
        /// </summary>
        public bool? CollapseSingleChildDirectories { get; init; }

        /// <summary>
        /// Maximum number of items to display per directory. When set, excess entries are replaced
        /// with a placeholder node (e.g., "… (+N more)"). Null means unlimited.
        /// </summary>
        public int? MaxItemsPerDirectory { get; init; }

        /// <summary>
        /// If true, only directories are rendered; files are skipped. Filtering rules still apply.
        /// </summary>
        public bool? DirectoriesOnly { get; init; }

        /// <summary>
        /// Profiles allow defining named presets. Each profile is a partial configuration
        /// that overrides values from the root configuration when selected via CLI.
        /// Example: "fast", "full", "csharp".
        /// </summary>
        public IReadOnlyDictionary<string, ContextConfigDto>? Profiles { get; init; }

        /// <summary>Optional history block to include recent Git changes in the output.</summary>
        public HistoryDto? History { get; init; }

        /// <summary>Optional content block to render file contents below file nodes.</summary>
        public ContentDto? Content { get; init; }
    }
}