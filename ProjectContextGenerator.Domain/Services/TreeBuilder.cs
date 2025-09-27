using ProjectContextGenerator.Domain.Abstractions;
using ProjectContextGenerator.Domain.Models;
using ProjectContextGenerator.Domain.Options;

namespace ProjectContextGenerator.Domain.Services
{
    /// <summary>
    /// Builds a hierarchical tree of directories and files starting from a root path.
    /// This implementation delegates visibility decisions (include/exclude) to an <see cref="IPathFilter"/>.
    /// A compatibility constructor is provided to accept an <see cref="IPathMatcher"/> and adapt it.
    /// </summary>
    /// <remarks>
    /// Preferred constructor using a high-level <see cref="IPathFilter"/>.
    /// </remarks>
    /// <param name="fs">Filesystem abstraction used to enumerate directories and files.</param>
    /// <param name="filter">Policy that decides whether paths should be included.</param>
    public sealed class TreeBuilder(IFileSystem fs, IPathFilter filter) : ITreeBuilder
    {
        private string _scanRoot = string.Empty;

        ///// <summary>
        ///// Backward-compatible constructor that adapts an <see cref="IPathMatcher"/> into an <see cref="IPathFilter"/>.
        ///// This preserves existing callers until they migrate to the filtered composition (globs + .gitignore).
        ///// </summary>
        ///// <param name="fs">Filesystem abstraction used to enumerate directories and files.</param>
        ///// <param name="matcher">Glob-based matcher (legacy path). Will be wrapped in a simple filter.</param>
        //public TreeBuilder(IFileSystem fs, IPathMatcher matcher)
        //    : this(fs, new PathMatcherFilterAdapter(matcher))
        //{
        //}
        /// <inheritdoc />
        public DirectoryNode Build(string rootPath, TreeScanOptions options)
        {
            _scanRoot = rootPath; // retained to compute relative paths
            var children = BuildChildren(rootPath, depth: 0, options);
            return new DirectoryNode(fs.GetFileName(rootPath), children);
        }

        private IReadOnlyList<TreeNode> BuildChildren(string absolutePath, int depth, TreeScanOptions o)
        {
            // Depth limit: when MaxDepth >= 0, stop once we've reached it.
            if (o.MaxDepth >= 0 && depth >= o.MaxDepth) return [];

            // Enumerate immediate children
            var dirs = fs.EnumerateDirectories(absolutePath);
            var files = o.DirectoriesOnly
                ? []
                : fs.EnumerateFiles(absolutePath);

            // Convert to relative forward-slash paths and apply traversal filter (ignores include globs)
            var traversableDirs = dirs.Where(d =>
            {
                var rel = ToRelative(d);
                return filter.CanTraverseDirectory(rel);
            }).ToList();

            if (!o.DirectoriesOnly)
            {
                files = files.Where(f =>
                {
                    var rel = ToRelative(f);
                    return filter.ShouldIncludeFile(rel);
                });
            }

            var children = new List<TreeNode>();

            // Recurse into traversable directories; render them only if explicitly included or non-empty after recursion
            foreach (var dirAbs in traversableDirs)
            {
                var relDir = ToRelative(dirAbs);
                var sub = new DirectoryNode(
                    fs.GetFileName(dirAbs),
                    BuildChildren(dirAbs, depth + 1, o)
                );

                // Decide if the directory should be kept:
                // - keep if explicitly included by filter, OR
                // - keep if it contains at least one included child
                var keepDirectory = filter.ShouldIncludeDirectory(relDir) || sub.Children.Count > 0;
                if (!keepDirectory)
                {
                    continue;
                }


                // Collapse single-child directory chains if enabled
                if (o.CollapseSingleChildDirectories &&
                    sub.Children.Count == 1 &&
                    sub.Children[0] is DirectoryNode onlyDir)
                {
                    sub = new DirectoryNode($"{sub.Name}/{onlyDir.Name}", onlyDir.Children);
                }

                children.Add(sub);
            }

            // Add files
            foreach (var fileAbs in files)
            {
                children.Add(new FileNode(fs.GetFileName(fileAbs)));
            }

            // Optional ordering: directories first, then files; always by name within each group
            if (o.SortDirectoriesFirst)
            {
                children = children
                    .OrderBy(n => n is FileNode) // directories (false) before files (true)
                    .ThenBy(n => n.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            // Optional cap per directory with placeholder summary node
            if (o.MaxItemsPerDirectory is int cap && children.Count > cap)
            {
                children = children
                    .Take(cap)
                    .Append(new FileNode($"… (+{children.Count - cap} more)"))
                    .ToList();
            }

            return children;
        }

        private string ToRelative(string absolutePath)
        {
            // Normalize to forward slashes and remove any leading "./" for filter contracts.
            var rel = Path.GetRelativePath(_scanRoot, absolutePath).Replace('\\', '/');
            if (rel.StartsWith("./"))
                rel = rel[2..];
            // Do not force a trailing slash; consumers (filter/matchers) handle directories appropriately.
            return rel;
        }

        /// <summary>
        /// Minimal adapter that wraps an <see cref="IPathMatcher"/> in an <see cref="IPathFilter"/>.
        /// This preserves legacy behavior (globs only) and does not apply .gitignore semantics.
        /// </summary>
        private sealed class PathMatcherFilterAdapter(IPathMatcher matcher) : IPathFilter
        {
            public bool ShouldIncludeDirectory(string relativePath)
                => matcher.IsMatch(Norm(relativePath), isDirectory: true);

            public bool ShouldIncludeFile(string relativePath)
                => matcher.IsMatch(Norm(relativePath), isDirectory: false);

            public bool CanTraverseDirectory(string relativePath)
                 => matcher.IsMatch(Norm(relativePath), isDirectory: true);

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