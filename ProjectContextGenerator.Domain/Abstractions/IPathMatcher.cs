namespace ProjectContextGenerator.Domain.Abstractions
{
    /// <summary>
    /// Defines a contract for matching paths against include/exclude rules.
    /// </summary>
    public interface IPathMatcher
    {
        /// <summary>
        /// Determines whether the specified relative path should be included.
        /// </summary>
        /// <param name="relativePath">Path relative to the root.</param>
        /// <param name="isDirectory">True if the path is a directory.</param>
        /// <returns>True if the path is allowed, otherwise false.</returns>
        bool IsMatch(string relativePath, bool isDirectory);
    }
}
