using ProjectContextGenerator.Domain.Abstractions;
using ProjectContextGenerator.Domain.Options;

namespace ProjectContextGenerator.Infrastructure.GitIgnore
{
    /// <summary>
    /// Loads and compiles .gitignore rules using an <see cref="IFileSystem"/> for I/O.
    /// Supports root-only and nested modes.
    /// </summary>
    /// <remarks>
    /// Creates a new provider that reads ignore files through the given file system abstraction.
    /// </remarks>
    /// <param name="fileSystem">Filesystem abstraction for reading files.</param>
    public sealed class GitIgnoreRuleProvider(IFileSystem fileSystem) : IIgnoreRuleProvider
    {

        /// <summary>
        /// Loads and compiles ignore rules according to <see cref="IgnoreLoadingOptions"/>.
        /// If no file is found or <see cref="IgnoreLoadingOptions.Mode"/> is <see cref="GitIgnoreMode.None"/>,
        /// an empty rule set is returned.
        /// </summary>
        public IIgnoreRuleSet Load(string rootPath, IgnoreLoadingOptions options)
        {
            if (options.Mode == GitIgnoreMode.None)
                return EmptyIgnoreRuleSet.Instance;

            var fileName = string.IsNullOrWhiteSpace(options.GitIgnoreFileName)
                ? ".gitignore"
                : options.GitIgnoreFileName;

            // Root-only: load a single file at <root>/.gitignore.
            if (options.Mode == GitIgnoreMode.RootOnly)
            {
                var rootIgnore = Combine(rootPath, fileName);
                if (!fileSystem.FileExists(rootIgnore))
                    return EmptyIgnoreRuleSet.Instance;

                var content = fileSystem.ReadAllText(rootIgnore);
                var rules = GitIgnoreParser.Parse(content);
                if (rules.Count == 0)
                    return EmptyIgnoreRuleSet.Instance;

                return new GitIgnoreRuleSet(rules);
            }

            // Nested: load .gitignore files from root and all subdirectories.
            // Order matters: parent directories first, then children.
            var scoped = new List<(string Scope, GitIgnoreParser.GitIgnoreRule Rule)>(capacity: 256);

            // BFS over directories to ensure parent-before-child ordering (deterministic).
            var queue = new Queue<string>();
            queue.Enqueue(rootPath);

            while (queue.Count > 0)
            {
                var dir = queue.Dequeue();

                // 1) Load .gitignore in this directory (scope = relative dir from root, "" for root)
                var ignorePath = Combine(dir, fileName);
                if (fileSystem.FileExists(ignorePath))
                {
                    var content = fileSystem.ReadAllText(ignorePath);
                    if (!string.IsNullOrWhiteSpace(content))
                    {
                        var rules = GitIgnoreParser.Parse(content);
                        if (rules.Count > 0)
                        {
                            var scope = Rel(rootPath, dir);
                            scoped.AddRange(rules.Select(r => (scope, r)));
                        }
                    }
                }

                // 2) Enqueue children
                foreach (var childDir in fileSystem.EnumerateDirectories(dir))
                    queue.Enqueue(childDir);
            }

            if (scoped.Count == 0)
                return EmptyIgnoreRuleSet.Instance;

            return new GitIgnoreRuleSet(scoped);
        }

        private static string Combine(string left, string right)
        {
            var p = Path.Combine(left, right);
            return p;
        }

        private static string Rel(string root, string abs)
        {
            var rel = Path.GetRelativePath(root, abs).Replace('\\', '/').Trim('/');
            // Normalize "." to empty scope
            return rel == "." ? "" : rel;
        }
    }
}