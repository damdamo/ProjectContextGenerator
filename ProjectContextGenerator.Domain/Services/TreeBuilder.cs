using ProjectContextGenerator.Domain.Abstractions;
using ProjectContextGenerator.Domain.Models;
using ProjectContextGenerator.Domain.Options;

namespace ProjectContextGenerator.Domain.Services
{
    public sealed class TreeBuilder(IFileSystem fs, IPathFilter filter) : ITreeBuilder
    {
        private string _scanRoot = string.Empty;

        public DirectoryNode Build(string rootPath, TreeScanOptions options)
        {
            _scanRoot = rootPath;
            var result = BuildChildrenEx(rootPath, depth: 0, options);
            return new DirectoryNode(fs.GetFileName(rootPath), result.Children);
        }

        // Small internal result to bubble up whether a subtree contains any included file.
        private readonly record struct TraversalResult(IReadOnlyList<TreeNode> Children, bool HasIncludedFileInSubtree);

        private TraversalResult BuildChildrenEx(string absolutePath, int depth, TreeScanOptions o)
        {
            // Depth limit: when MaxDepth >= 0, stop once we've reached it.
            if (o.MaxDepth >= 0 && depth >= o.MaxDepth)
                return new TraversalResult([], HasIncludedFileInSubtree: false);

            // Enumerate immediate directories (we’ll decide traversal with CanTraverseDirectory)
            var allDirs = fs.EnumerateDirectories(absolutePath).ToList();

            // Enumerate files ALWAYS (even if DirectoriesOnly) so we can decide whether to keep the folder
            var allFiles = fs.EnumerateFiles(absolutePath).ToList();

            // Included files in THIS directory (used both for rendering and for keeping directories)
            var includedFilesHere = allFiles.Where(f =>
            {
                var rel = ToRelative(f);
                return filter.ShouldIncludeFile(rel);
            }).ToList();

            // Directories we can traverse into (ignores include globs)
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

                // Decide if this directory node should be kept
                var explicitlyIncluded = filter.ShouldIncludeDirectory(relDir);
                var keepDirectory = explicitlyIncluded || sub.HasIncludedFileInSubtree || sub.Children.Count > 0;

                if (!keepDirectory)
                    continue;

                // Create the directory node with its (possibly pruned) children
                DirectoryNode dirNode = new(fs.GetFileName(dirAbs), sub.Children);

                // Collapse single-child directory chain if enabled
                if (o.CollapseSingleChildDirectories &&
                    dirNode.Children.Count == 1 &&
                    dirNode.Children[0] is DirectoryNode onlyDir)
                {
                    dirNode = new DirectoryNode($"{dirNode.Name}/{onlyDir.Name}", onlyDir.Children);
                }

                children.Add(dirNode);

                // Propagate presence of included files up the tree
                if (sub.HasIncludedFileInSubtree)
                    subtreeHasIncludedFile = true;
            }

            // Add files (only when DirectoriesOnly == false)
            if (!o.DirectoriesOnly)
            {
                foreach (var fileAbs in includedFilesHere)
                {
                    children.Add(new FileNode(fs.GetFileName(fileAbs)));
                }
            }

            // Optional ordering
            if (o.SortDirectoriesFirst)
            {
                children = children
                    .OrderBy(n => n is FileNode) // directories (false) before files (true)
                    .ThenBy(n => n.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            // Optional cap with placeholder
            if (o.MaxItemsPerDirectory is int cap && children.Count > cap)
            {
                children = children
                    .Take(cap)
                    .Append(new FileNode($"… (+{children.Count - cap} more)"))
                    .ToList();
            }

            return new TraversalResult(children, subtreeHasIncludedFile);
        }

        private string ToRelative(string absolutePath)
        {
            var rel = Path.GetRelativePath(_scanRoot, absolutePath).Replace('\\', '/');
            if (rel.StartsWith("./"))
                rel = rel[2..];
            return rel;
        }

        // Adapter remains unchanged, but must implement CanTraverseDirectory for legacy path-matcher usage
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