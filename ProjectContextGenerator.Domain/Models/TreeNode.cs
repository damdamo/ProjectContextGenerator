namespace ProjectContextGenerator.Domain.Models
{
    /// <summary>
    /// Base type for all nodes in a directory tree representation.
    /// Can represent either a file or a directory.
    /// </summary>
    /// <param name="Name">The display name of the node (file or directory name).</param>
    public abstract record TreeNode(string Name);

    /// <summary>
    /// Represents a directory node in the tree, containing child nodes.
    /// </summary>
    /// <param name="Name">The name of the directory.</param>
    /// <param name="Children">The collection of child nodes within this directory.</param>
    public sealed record DirectoryNode(string Name, IReadOnlyList<TreeNode> Children) : TreeNode(Name);

    /// <summary>
    /// Represents a file node in the tree (a leaf node).
    /// </summary>
    /// <param name="Name">The name of the file.</param>
    public sealed record FileNode(string Name) : TreeNode(Name);
}
