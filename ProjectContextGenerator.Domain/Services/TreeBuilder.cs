using ProjectContextGenerator.Domain.Abstractions;
using ProjectContextGenerator.Domain.Models;
using ProjectContextGenerator.Domain.Options;

namespace ProjectContextGenerator.Domain.Services
{
    public sealed class TreeBuilder(IFileSystem fs) : ITreeBuilder
    {
        public DirectoryNode Build(string rootPath, TreeScanOptions o)
            => new(fs.GetFileName(rootPath), BuildChildren(rootPath, 0, o));

        private IReadOnlyList<TreeNode> BuildChildren(string path, int depth, TreeScanOptions o)
        {
            if (o.MaxDepth >= 0 && depth >= o.MaxDepth) return [];

            var dirs = fs.EnumerateDirectories(path);
            var files = fs.EnumerateFiles(path);

            // TODO: filtrage Include/Exclude via globs
            // TODO: tri

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
