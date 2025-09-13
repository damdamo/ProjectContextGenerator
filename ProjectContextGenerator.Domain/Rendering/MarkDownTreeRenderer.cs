using ProjectContextGenerator.Domain.Abstractions;
using ProjectContextGenerator.Domain.Models;

namespace ProjectContextGenerator.Domain.Rendering
{
    public sealed class MarkdownTreeRenderer : ITreeRenderer
    {
        public string Render(DirectoryNode root)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"/{root.Name}/");
            RenderChildren(root.Children, 0, sb);
            return sb.ToString();
        }

        private static void RenderChildren(IReadOnlyList<TreeNode> nodes, int level, System.Text.StringBuilder sb)
        {
            foreach (var n in nodes)
            {
                var indent = new string(' ', level * 2);
                switch (n)
                {
                    case DirectoryNode d:
                        sb.AppendLine($"{indent}- {d.Name}/");
                        RenderChildren(d.Children, level + 1, sb);
                        break;
                    case FileNode f:
                        sb.AppendLine($"{indent}- {f.Name}");
                        break;
                }
            }
        }
    }

}
