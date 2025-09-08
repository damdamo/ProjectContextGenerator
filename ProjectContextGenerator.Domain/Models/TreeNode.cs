namespace ProjectContextGenerator.Domain.Models
{
    // Domain/Models/TreeNode.cs
    public abstract record TreeNode(string Name);
    public sealed record DirectoryNode(string Name, IReadOnlyList<TreeNode> Children) : TreeNode(Name);
    public sealed record FileNode(string Name) : TreeNode(Name);
}
