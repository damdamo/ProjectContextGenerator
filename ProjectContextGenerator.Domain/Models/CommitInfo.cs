using ProjectContextGenerator.Domain.Models;
using System.Reflection;

namespace ProjectContextGenerator.Domain.Models
{
    /// <summary>
    /// Minimal commit information used for "Recent Changes" rendering.
    /// </summary>
    public sealed class CommitInfo
    {
        public required string Hash { get; init; }
        public required string AuthorName { get; init; }
        public required string AuthorEmail { get; init; }
        /// <summary>ISO-8601 commit date (committer date), e.g. 2025-09-26T21:03:45+02:00</summary>
        public required string DateIso { get; init; }
        public required string Title { get; init; }
        /// <summary>Already truncated lines from the commit body (max MaxBodyLines).</summary>
        public required IReadOnlyList<string> BodyLines { get; init; }
    }
}
