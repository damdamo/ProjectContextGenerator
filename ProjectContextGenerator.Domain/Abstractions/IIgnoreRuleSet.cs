namespace ProjectContextGenerator.Domain.Abstractions
{
    /// <summary>
    /// A compiled, immutable set of ignore rules with .gitignore-like semantics.
    /// Responsibilities:
    ///  - Interpret patterns with order sensitivity (last rule wins).
    ///  - Support negation rules (patterns starting with '!').
    ///  - Distinguish directory-vs-file matching where applicable.
    /// This interface is pure logic: no file I/O.
    /// </summary>
    public interface IIgnoreRuleSet
    {
        /// <summary>
        /// Returns true if the given relative path would be ignored by this rule set.
        /// <paramref name="relativePath"/> must use forward slashes ('/') and be relative to the scan root.
        /// </summary>
        /// <param name="relativePath">Path relative to the scan root, using '/'.</param>
        /// <param name="isDirectory">True if the path is a directory.</param>
        bool IsIgnored(string relativePath, bool isDirectory);
    }
}
