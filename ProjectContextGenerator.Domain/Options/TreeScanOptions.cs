namespace ProjectContextGenerator.Domain.Options
{
    /// <summary>
    /// Options controlling how a directory tree is scanned and built.
    /// </summary>
    /// <param name="MaxDepth">
    /// Maximum depth of recursion when scanning the tree.
    /// A value of 0 scans only the root, 1 includes its immediate children, etc.
    /// Use -1 for unlimited depth. Default is 4.
    /// </param>
    /// <param name="IncludeGlobs">
    /// Glob patterns specifying which files/directories to include.
    /// Example: ["**/*.cs", "**/*.csproj"].
    /// If null, everything is included by default.
    /// </param>
    /// <param name="ExcludeGlobs">
    /// Glob patterns specifying which files/directories to exclude.
    /// Example: ["**/bin/**", "**/obj/**", "**/node_modules/**"].
    /// </param>
    /// <param name="SortDirectoriesFirst">
    /// If true, directories are listed before files within each folder.
    /// Default is true.
    /// </param>
    /// <param name="CollapseSingleChildDirectories">
    /// If true, chains of directories with only a single child are collapsed
    /// into a single combined node for readability.
    /// Example:
    ///   Without collapsing:
    ///     src/ → utils/ → helpers/ → core/ → File.cs
    ///   With collapsing:
    ///     src/utils/helpers/core/ → File.cs
    /// Default is true.
    /// </param>
    /// <param name="MaxItemsPerDirectory">
    /// Optional cap on the number of items per directory.
    /// If set, extra items are replaced with a placeholder node
    /// (e.g. "… (+N more)"). Null means no limit.
    /// </param>
    /// <param name="GitIgnore">
    /// Controls whether and how .gitignore files are applied during scanning.
    /// Default is RootOnly for practical out-of-the-box filtering.
    /// </param>
    /// <param name="GitIgnoreFileName">
    /// The filename used to locate the ignore file at the root (usually ".gitignore").
    /// Overridable for tests or custom scenarios.
    /// </param>
    public sealed record TreeScanOptions(
        int MaxDepth = 4,
        IReadOnlyList<string>? IncludeGlobs = null,   // e.g., ["**/*.cs", "**/*.csproj"]
        IReadOnlyList<string>? ExcludeGlobs = null,   // e.g., ["**/bin/**", "**/obj/**", "**/node_modules/**"]
        bool SortDirectoriesFirst = true,
        bool CollapseSingleChildDirectories = true,
        int? MaxItemsPerDirectory = null,
        GitIgnoreMode GitIgnore = GitIgnoreMode.RootOnly,
        string? GitIgnoreFileName = ".gitignore"
    );
}
