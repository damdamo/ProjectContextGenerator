using ProjectContextGenerator.Domain.Abstractions;
using ProjectContextGenerator.Domain.Models;
using ProjectContextGenerator.Domain.Options;

namespace ProjectContextGenerator.Infrastructure.History
{
    /// <summary>
    /// Git-backed history provider using "git log".
    /// Returns an empty list when git is unavailable or the directory is not a git repository.
    /// </summary>
    public sealed class GitHistoryProvider(IProcessRunner runner) : IHistoryProvider
    {
        public IReadOnlyList<CommitInfo> GetRecentCommits(HistoryOptions options, string workingDirectory)
        {
            // Build git log command
            // %x1f is the unit separator for robust parsing of fields.
            var pretty = "%H%x1f%an%x1f%ae%x1f%cI%x1f%s%x1f%b";
            var merges = options.IncludeMerges ? "" : " --no-merges";
            var args = $"log --max-count={Math.Max(0, options.Last)}{merges} --date=iso-strict --pretty=format:{pretty}";

            var result = runner.Run("git", args, workingDirectory, TimeSpan.FromSeconds(10));
            if (result.ExitCode != 0)
            {
                // Not a git repo or git missing → fail silently & return empty
                return [];
            }

            var commits = new List<CommitInfo>();
            // Each commit is separated by a blank line *unless* body is multi-line; safer approach:
            // We will split on lines, accumulate until we have at least 6 tokens when joined by US.
            // Instead, rely on the fact that %b may contain newlines; so we parse stream-wise:
            var lines = result.StdOut.Replace("\r\n", "\n").Split('\n');
            // Reconstruct records by detecting boundaries using the first five fields which are single-line.
            // We'll accumulate lines until we can split into at least 6 fields once.

            var buffer = new List<string>();
            void FlushBuffer()
            {
                if (buffer.Count == 0) return;
                var record = string.Join('\n', buffer);
                var idx = record.Contains('\u001f'); // unit separator exists; but body may include none
                // Split all on US (should give at least 6 segments; the last is the whole body which can have newlines)
                var parts = record.Split('\u001f');
                if (parts.Length >= 6)
                {
                    var hash = parts[0].Trim();
                    var an = parts[1].Trim();
                    var ae = parts[2].Trim();
                    var date = parts[3].Trim();
                    var title = parts[4].Trim();
                    var bodyRaw = parts[5];
                    var bodyLines = bodyRaw.Replace("\r\n", "\n").Split('\n', StringSplitOptions.None)
                                           .Where(l => !string.IsNullOrWhiteSpace(l))
                                           .Take(Math.Max(0, options.MaxBodyLines))
                                           .ToArray();
                    commits.Add(new CommitInfo
                    {
                        Hash = hash,
                        AuthorName = an,
                        AuthorEmail = ae,
                        DateIso = date,
                        Title = title,
                        BodyLines = bodyLines
                    });
                }
                buffer.Clear();
            }

            foreach (var line in lines)
            {
                // We can't just split commits by empty line because %b can contain empties.
                // Trick: the first 5 fields (H, an, ae, cI, s) never contain '\n' — they come on one line together.
                // git writes exactly one line for the whole pretty string; but %b appends the body on following lines.
                // So we detect the START of a new record by a line that contains AT LEAST four unit separators.
                var usCount = line.Count(c => c == '\u001f');
                if (usCount >= 4)
                {
                    // Start of a new commit; flush previous
                    FlushBuffer();
                }
                buffer.Add(line);
            }
            FlushBuffer();

            return commits;
        }
    }
}
