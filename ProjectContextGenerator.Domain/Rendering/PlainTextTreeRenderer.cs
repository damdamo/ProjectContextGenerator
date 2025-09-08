using ProjectContextGenerator.Domain.Abstractions;
using ProjectContextGenerator.Domain.Models;

namespace ProjectContextGenerator.Domain.Rendering
{
    public sealed class PlainTextTreeRenderer : ITreeRenderer
    {
        public string Render(DirectoryNode root)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"/{root.Name}");
            RenderChildren(root.Children, 0, sb);
            return sb.ToString();
        }

        private static void RenderChildren(IReadOnlyList<TreeNode> nodes, int level, System.Text.StringBuilder sb)
        {
            foreach (var n in nodes)
            {
                var indent = new string(' ', level * 2);
                var suffix = n is DirectoryNode ? "/" : "";
                sb.AppendLine($"{indent}{n.Name}{suffix}");
                if (n is DirectoryNode d)
                    RenderChildren(d.Children, level + 1, sb);
            }
        }
    }

}
