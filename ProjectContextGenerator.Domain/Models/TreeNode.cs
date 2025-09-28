namespace ProjectContextGenerator.Domain.Models
{
    /// <summary>
    /// Base type for all nodes in a directory tree representation.
    /// Can represent either a file or a directory.
    /// </summary>
    /// <param name="Name">The display name of the node (file or directory name).</param>
    /// <param name="RelativePath">
    /// Path relative to the scan root using forward slashes.
    /// The root directory node uses an empty string "".
    /// </param>
    public abstract record TreeNode(string Name, string RelativePath);

    /// <summary>
    /// Represents a directory node in the tree, containing child nodes.
    /// </summary>
    /// <param name="Name">The name of the directory.</param>
    /// <param name="RelativePath">Directory path relative to the scan root (e.g., "src/lib").</param>
    /// <param name="Children">The collection of child nodes within this directory.</param>
    public sealed record DirectoryNode(string Name, string RelativePath, IReadOnlyList<TreeNode> Children) : TreeNode(Name, RelativePath);

    /// <summary>
    /// Represents a file node in the tree (a leaf node).
    /// </summary>
    /// <param name="Name">The name of the file.</param>
    /// <param name="RelativePath">File path relative to the scan root (e.g., "src/lib/File.cs").</param>
    public sealed record FileNode(string Name, string RelativePath) : TreeNode(Name, RelativePath);
}
