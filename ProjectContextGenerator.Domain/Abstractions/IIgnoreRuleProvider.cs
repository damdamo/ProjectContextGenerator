using ProjectContextGenerator.Domain.Options;

namespace ProjectContextGenerator.Domain.Abstractions
{
    /// <summary>
    /// Loads ignore rules from one or more sources (e.g., a root .gitignore file).
    /// Implementations may read from the filesystem (via IFileSystem) and parse
    /// the patterns into a compiled <see cref="IIgnoreRuleSet"/>.
    /// </summary>
    public interface IIgnoreRuleProvider
    {
        /// <summary>
        /// Loads and compiles ignore rules for a given root path according to the provided options.
        /// Implementations MUST NOT throw if no ignore file is found; they should return an empty rule set.
        /// </summary>
        /// <param name="rootPath">Absolute path to the repository/project root.</param>
        /// <param name="options">Loading options that define mode and file names.</param>
        IIgnoreRuleSet Load(string rootPath, IgnoreLoadingOptions options);
    }
}
