using ProjectContextGenerator.Domain.Abstractions;
using ProjectContextGenerator.Domain.Models;
using ProjectContextGenerator.Domain.Options;

namespace ProjectContextGenerator.Domain.Services
{
    public sealed class TreeBuilder(IFileSystem fs, IPathMatcher matcher) : ITreeBuilder
    {
        private string _scanRoot = string.Empty;

        public DirectoryNode Build(string rootPath, TreeScanOptions o)
        {
            _scanRoot = rootPath; // keep for relative-path filtering
            return new DirectoryNode(fs.GetFileName(rootPath), BuildChildren(rootPath, 0, o));
        }

        private IReadOnlyList<TreeNode> BuildChildren(string path, int depth, TreeScanOptions o)
        {
            if (o.MaxDepth >= 0 && depth >= o.MaxDepth) return [];

            var dirs = fs.EnumerateDirectories(path);
            var files = fs.EnumerateFiles(path);

            dirs = dirs.Where(d =>
            {
                var rel = Path.GetRelativePath(_scanRoot, d).Replace('\\', '/');
                if (!rel.EndsWith('/')) rel += "/";            // directories match better with trailing slash
                return matcher.IsMatch(rel, isDirectory: true);
            });

            files = files.Where(f =>
            {
                var rel = Path.GetRelativePath(_scanRoot, f).Replace('\\', '/');
                return matcher.IsMatch(rel, isDirectory: false);
            });

            var children = new List<TreeNode>();

            foreach (var d in dirs)
            {
                var sub = new DirectoryNode(
                    fs.GetFileName(d),
                    BuildChildren(d, depth + 1, o)
                );

                // Collapse option
                if (o.CollapseSingleChildDirectories &&
                    sub.Children.Count == 1 &&
                    sub.Children[0] is DirectoryNode onlyDir)
                {
                    sub = new DirectoryNode($"{sub.Name}/{onlyDir.Name}", onlyDir.Children);
                }

                children.Add(sub);
            }

            foreach (var f in files)
                children.Add(new FileNode(fs.GetFileName(f)));

            if (o.SortDirectoriesFirst)
                children = children.OrderBy(n => n is FileNode).ThenBy(n => n.Name, StringComparer.OrdinalIgnoreCase).ToList();

            if (o.MaxItemsPerDirectory is int cap && children.Count > cap)
                children = children.Take(cap).Append(new FileNode($"… (+{children.Count - cap} more)")).ToList();

            return children;
        }
    }

}
