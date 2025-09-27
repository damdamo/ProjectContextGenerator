using System.Text;
using ProjectContextGenerator.Domain.Models;
using ProjectContextGenerator.Domain.Options;
using ProjectContextGenerator.Domain.Abstractions;

namespace ProjectContextGenerator.Domain.Rendering
{
    public sealed class MarkdownHistoryRenderer : IHistoryRenderer
    {
        public string Render(IReadOnlyList<CommitInfo> commits, HistoryOptions options)
        {
            if (commits.Count == 0) return string.Empty;
            var sb = new StringBuilder();
            sb.AppendLine($"## Recent Changes (last {options.Last})");
            foreach (var c in commits)
            {
                sb.Append("- ").AppendLine(c.Title);
                if (options.Detail == HistoryDetail.TitleAndBody && c.BodyLines.Count > 0)
                {
                    foreach (var line in c.BodyLines)
                    {
                        sb.Append("  ").AppendLine(line);
                    }
                }
            }
            return sb.ToString().TrimEnd();
        }
    }
}
