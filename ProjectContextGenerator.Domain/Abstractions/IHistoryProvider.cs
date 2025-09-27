using ProjectContextGenerator.Domain.Models;
using ProjectContextGenerator.Domain.Options;

namespace ProjectContextGenerator.Domain.Abstractions
{
    public interface IHistoryProvider
    {
        /// <summary>
        /// Returns the recent commits for the current Git repository (located at <paramref name="workingDirectory"/>).
        /// If the directory is not a Git repository or Git is not available, returns an empty list.
        /// </summary>
        IReadOnlyList<CommitInfo> GetRecentCommits(HistoryOptions options, string workingDirectory);
    }
}
