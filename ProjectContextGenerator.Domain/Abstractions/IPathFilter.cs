namespace ProjectContextGenerator.Domain.Abstractions
{
    /// <summary>
    /// High-level policy for path visibility during tree building.
    /// Combines include globs, .gitignore semantics, and exclude globs
    /// into a single decision point for the TreeBuilder.
    /// </summary>
    public interface IPathFilter
    {
        /// <summary>
        /// Returns true if the directory should be traversed/rendered.
        /// The given path MUST be relative to the scan root and use forward slashes ('/').
        /// </summary>
        bool ShouldIncludeDirectory(string relativePath);

        /// <summary>
        /// Returns true if the file should be rendered.
        /// The given path MUST be relative to the scan root and use forward slashes ('/').
        /// </summary>
        bool ShouldIncludeFile(string relativePath);

        /// <summary>
        /// Returns true if the directory is allowed to be traversed (descended into).
        /// This method MUST ignore include globs. It should only block traversal for
        /// directories that are excluded by ExcludeGlobs or ignored by .gitignore.
        /// The given path MUST be relative to the scan root and use forward slashes ('/').
        /// </summary>
        bool CanTraverseDirectory(string relativePath);
    }
}
