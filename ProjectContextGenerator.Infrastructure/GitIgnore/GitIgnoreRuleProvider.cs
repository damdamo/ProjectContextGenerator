using ProjectContextGenerator.Domain.Abstractions;
using ProjectContextGenerator.Domain.Options;

namespace ProjectContextGenerator.Infrastructure.GitIgnore
{
    /// <summary>
    /// Loads and compiles .gitignore rules using an <see cref="IFileSystem"/> for I/O.
    /// V1 intentionally supports only a single root .gitignore file; nested files are future work.
    /// </summary>
    /// <remarks>
    /// Creates a new provider that reads ignore files through the given file system abstraction.
    /// </remarks>
    /// <param name="fileSystem">Filesystem abstraction for reading files.</param>
    public sealed class GitIgnoreRuleProvider(IFileSystem fileSystem) : IIgnoreRuleProvider
    {
        private readonly IFileSystem _fileSystem = fileSystem;

        /// <summary>
        /// Loads and compiles ignore rules according to <see cref="IgnoreLoadingOptions"/>.
        /// If no file is found or <see cref="IgnoreLoadingOptions.Mode"/> is <see cref="GitIgnoreMode.None"/>,
        /// an empty rule set is returned.
        /// </summary>
        public IIgnoreRuleSet Load(string rootPath, IgnoreLoadingOptions options)
        {
            if (options.Mode == GitIgnoreMode.None)
                return EmptyIgnoreRuleSet.Instance;

            // V1: RootOnly; if Nested is requested, we currently behave like RootOnly.
            var fileName = string.IsNullOrWhiteSpace(options.GitIgnoreFileName)
                ? ".gitignore"
                : options.GitIgnoreFileName;

            var ignorePath = Path.Combine(rootPath, fileName);

            if (!_fileSystem.FileExists(ignorePath))
                return EmptyIgnoreRuleSet.Instance;

            var content = _fileSystem.ReadAllText(ignorePath);
            if (string.IsNullOrWhiteSpace(content))
                return EmptyIgnoreRuleSet.Instance;

            var rules = GitIgnoreParser.Parse(content);
            if (rules.Count == 0)
                return EmptyIgnoreRuleSet.Instance;

            return new GitIgnoreRuleSet(rules);
        }
    }
}
