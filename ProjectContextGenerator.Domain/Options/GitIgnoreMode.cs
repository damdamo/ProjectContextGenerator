namespace ProjectContextGenerator.Domain.Options
{
    /// <summary>
    /// Controls how .gitignore rules are considered during scanning.
    /// </summary>
    public enum GitIgnoreMode
    {
        /// <summary>No .gitignore loading at all.</summary>
        None = 0,

        /// <summary>
        /// Load only the repository root .gitignore file (e.g., &lt;root&gt;/.gitignore).
        /// Nested .gitignore files are ignored.
        /// </summary>
        RootOnly = 1,

        /// <summary>
        /// Load the root .gitignore and also nested .gitignore files, applying scope-aware rules.
        /// (This is future-facing; initial implementation may not support it yet.)
        /// </summary>
        Nested = 2
    }
}
