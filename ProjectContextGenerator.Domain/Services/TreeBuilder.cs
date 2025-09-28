using ProjectContextGenerator.Domain.Abstractions;
using ProjectContextGenerator.Domain.Models;
using ProjectContextGenerator.Domain.Options;

namespace ProjectContextGenerator.Domain.Services
{
    /// <summary>
    /// Builds a directory tree model from a file system, honoring include/exclude rules
    /// and .gitignore semantics via the provided <see cref="IPathFilter"/>.
    /// </summary>
    public sealed class TreeBuilder(IFileSystem fs, IPathFilter filter) : ITreeBuilder
    {
        private string _scanRoot = string.Empty;

        /// <summary>
        /// Builds the directory tree for the specified root path using the given scan options.
        /// </summary>
        /// <param name="rootPath">Absolute path of the directory root to scan.</param>
        /// <param name="options">Scan options controlling traversal and pruning.</param>
        /// <returns>A <see cref="DirectoryNode"/> representing the root of the tree.</returns>
        public DirectoryNode Build(string rootPath, TreeScanOptions options)
        {
            _scanRoot = rootPath;
            var result = BuildChildrenEx(rootPath, depth: 0, options);
            // Root node uses empty relative path "" for convenience.
            return new DirectoryNode(fs.GetFileName(rootPath), "", result.Children);
        }

        /// <summary>
        /// Small internal result to propagate both the pruned children and whether
        /// the subtree contains any included files (useful to keep directory spines).
        /// </summary>
        private readonly record struct TraversalResult(IReadOnlyList<TreeNode> Children, bool HasIncludedFileInSubtree);

        /// <summary>
        /// Recursively builds children for a given directory, applying depth limits, filters,
        /// and directory-keeping rules.
        /// </summary>
        private TraversalResult BuildChildrenEx(string absolutePath, int depth, TreeScanOptions o)
        {
            // Stop at depth limit when MaxDepth >= 0.
            if (o.MaxDepth >= 0 && depth >= o.MaxDepth)
                return new TraversalResult([], HasIncludedFileInSubtree: false);

            var allDirs = fs.EnumerateDirectories(absolutePath).ToList();
            var allFiles = fs.EnumerateFiles(absolutePath).ToList();

            // Determine included files in THIS directory (used for rendering and for keeping folders).
            var includedFilesHere = allFiles.Where(f =>
            {
                var rel = ToRelative(f);
                return filter.ShouldIncludeFile(rel);
            }).ToList();

            // Determine which directories can be traversed (independent of include globs).
            var traversableDirs = allDirs.Where(d =>
            {
                var rel = ToRelative(d);
                return filter.CanTraverseDirectory(rel);
            }).ToList();

            var children = new List<TreeNode>();
            var subtreeHasIncludedFile = includedFilesHere.Count > 0;

            // Recurse into directories
            foreach (var dirAbs in traversableDirs)
            {
                var relDir = ToRelative(dirAbs);
                var sub = BuildChildrenEx(dirAbs, depth + 1, o);

                // Keep the directory if explicitly included, or if there are included files somewhere below,
                // or if it has any (already-kept) children after pruning.
                var explicitlyIncluded = filter.ShouldIncludeDirectory(relDir);
                var keepDirectory = explicitlyIncluded || sub.HasIncludedFileInSubtree || sub.Children.Count > 0;

                if (!keepDirectory)
                    continue;

                // Create the directory node. Relative path is the folder's path relative to the scan root.
                DirectoryNode dirNode = new(fs.GetFileName(dirAbs), relDir, sub.Children);

                // Optionally collapse single-child directory chains for readability.
                // When collapsing, keep the deepest relative path (the actual filesystem location).
                if (o.CollapseSingleChildDirectories &&
                    dirNode.Children.Count == 1 &&
                    dirNode.Children[0] is DirectoryNode onlyDir)
                {
                    dirNode = new DirectoryNode($"{dirNode.Name}/{onlyDir.Name}", onlyDir.RelativePath, onlyDir.Children);
                }

                children.Add(dirNode);

                if (sub.HasIncludedFileInSubtree)
                    subtreeHasIncludedFile = true;
            }

            // Add files unless DirectoriesOnly is requested.
            if (!o.DirectoriesOnly)
            {
                foreach (var fileAbs in includedFilesHere)
                {
                    var relFile = ToRelative(fileAbs);
                    children.Add(new FileNode(fs.GetFileName(fileAbs), relFile));
                }
            }

            // Optional ordering: directories first, then alphabetical within each kind.
            if (o.SortDirectoriesFirst)
            {
                children = children
                    .OrderBy(n => n is FileNode) // directories (false) before files (true)
                    .ThenBy(n => n.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            // Optional cap with placeholder node, preserving user feedback about omitted entries.
            if (o.MaxItemsPerDirectory is int cap && children.Count > cap)
            {
                children = children
                    .Take(cap)
                    .Append(new FileNode($"… (+{children.Count - cap} more)", "__ellipsis__"))
                    .ToList();
            }

            return new TraversalResult(children, subtreeHasIncludedFile);
        }

        /// <summary>
        /// Converts an absolute path under the current scan root to a forward-slash relative path.
        /// The root itself becomes an empty string "".
        /// </summary>
        private string ToRelative(string absolutePath
        )
        {
            var rel = Path.GetRelativePath(_scanRoot, absolutePath).Replace('\\', '/');
            if (rel.StartsWith("./"))
                rel = rel[2..];
            return rel == "." ? "" : rel;
        }

        /// <summary>
        /// Adapter to reuse legacy <see cref="IPathMatcher"/> implementations behind the <see cref="IPathFilter"/> facade.
        /// </summary>
        private sealed class PathMatcherFilterAdapter(IPathMatcher matcher) : IPathFilter
        {
            public bool CanTraverseDirectory(string relativePath)
                => matcher.IsMatch(Norm(relativePath), isDirectory: true);

            public bool ShouldIncludeDirectory(string relativePath)
                => matcher.IsMatch(Norm(relativePath), isDirectory: true);

            public bool ShouldIncludeFile(string relativePath)
                => matcher.IsMatch(Norm(relativePath), isDirectory: false);

            private static string Norm(string p)
            {
                var s = p.Replace('\\', '/');
                if (s.StartsWith("./"))
                    s = s[2..];
                return s;
            }
        }
    }
}