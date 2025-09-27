using ProjectContextGenerator.Domain.Models;
using ProjectContextGenerator.Domain.Options;

namespace ProjectContextGenerator.Domain.Abstractions
{
    public interface IHistoryRenderer
    {
        /// <summary>Renders the "Recent Changes" block given commits and options. Returns an empty string if commits is empty.</summary>
        string Render(IReadOnlyList<CommitInfo> commits, HistoryOptions options);
    }
}
