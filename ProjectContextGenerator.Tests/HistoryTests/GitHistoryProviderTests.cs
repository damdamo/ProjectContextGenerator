using System.Text;
using ProjectContextGenerator.Domain.Options;
using ProjectContextGenerator.Infrastructure.History;
using ProjectContextGenerator.Tests.Fakes;

namespace ProjectContextGenerator.Tests.HistoryTests
{
    public sealed class GitHistoryProviderTests
    {
        private static string US => "\u001f"; // unit separator

        private static string MakeLogRecord(
            string hash, string an, string ae, string cI, string subject, params string[] bodyLines)
        {
            // Header line contains 5 fields separated by US, then %b (body) starts after US and can span new lines
            var sb = new StringBuilder();
            sb.Append(hash).Append(US)
              .Append(an).Append(US)
              .Append(ae).Append(US)
              .Append(cI).Append(US)
              .Append(subject).Append(US);
            if (bodyLines.Length > 0)
            {
                sb.AppendLine(string.Join("\n", bodyLines));
            }
            return sb.ToString();
        }

        [Fact]
        public void Builds_Correct_Git_Log_Arguments_And_Respects_IncludeMerges()
        {
            var fake = new FakeProcessRunner();
            // No output needed; we only inspect arguments
            fake.EnqueueResult(exitCode: 0, stdout: "", stderr: "");
            var provider = new GitHistoryProvider(fake);
            var opts = new HistoryOptions(Last: 15, MaxBodyLines: 3, Detail: HistoryDetail.TitlesOnly, IncludeMerges: false);

            provider.GetRecentCommits(opts, workingDirectory: "/repo");
            Assert.NotNull(fake.LastInvocation);
            Assert.Equal("git", fake.LastInvocation!.FileName);
            Assert.Contains("--max-count=15", fake.LastInvocation.Arguments);
            Assert.Contains("--no-merges", fake.LastInvocation.Arguments);
            Assert.Contains("--pretty=format:%H%x1f%an%x1f%ae%x1f%cI%x1f%s%x1f%b", fake.LastInvocation.Arguments);
            Assert.Contains("--date=iso-strict", fake.LastInvocation.Arguments);
            Assert.Equal("/repo", fake.LastInvocation.WorkingDirectory);

            // With merges = true
            fake.Reset();
            fake.EnqueueResult(0, "", "");
            provider.GetRecentCommits(opts with { IncludeMerges = true }, "/repo");
            Assert.NotNull(fake.LastInvocation);
            Assert.DoesNotContain("--no-merges", fake.LastInvocation!.Arguments);
        }

        [Fact]
        public void Parses_Single_Commit_NoBody()
        {
            var fake = new FakeProcessRunner();
            var stdout = MakeLogRecord("h1", "alice", "a@ex", "2025-09-26T10:00:0002:00", "title 1");
            fake.EnqueueResult(0, stdout, "");

            var provider = new GitHistoryProvider(fake);
            var commits = provider.GetRecentCommits(new HistoryOptions(Last: 1, MaxBodyLines: 5), "/repo");
            Assert.Single(commits);
            var c = commits[0];
            Assert.Equal("h1", c.Hash);
            Assert.Equal("alice", c.AuthorName);
            Assert.Equal("a@ex", c.AuthorEmail);
            Assert.Equal("2025-09-26T10:00:0002:00", c.DateIso);
            Assert.Equal("title 1", c.Title);
            Assert.Empty(c.BodyLines);
        }

        [Fact]
        public void Parses_MultiLine_Body_Trims_Empty_And_Truncates()
        {
            var fake = new FakeProcessRunner();
            var stdout = new StringBuilder()
                            .Append(MakeLogRecord("h1", "bob", "b@ex", "2025-09-26T11:00:0002:00", "feat: x",
            "", "line1", "line2", "", "line3", "line4")) // includes empties
                            .ToString();
            fake.EnqueueResult(0, stdout, "");

            var provider = new GitHistoryProvider(fake);
            var commits = provider.GetRecentCommits(new HistoryOptions(Last: 1, MaxBodyLines: 3, Detail: HistoryDetail.TitleAndBody), "/repo");
            Assert.Single(commits);
            var c = commits[0];
            // empties removed, then truncated to 3
            Assert.Equal(["line1", "line2", "line3"], c.BodyLines.ToArray());
        }

        [Fact]
        public void Parses_Multiple_Commits_In_Order()
        {
            var fake = new FakeProcessRunner();
            var sb = new StringBuilder();
            sb.Append(MakeLogRecord("h1", "u1", "u1@ex", "2025-09-26T10:00:0002:00", "t1", "b1"));
            sb.AppendLine(); // next header line will start a new record anyway
            sb.Append(MakeLogRecord("h2", "u2", "u2@ex", "2025-09-26T11:00:0002:00", "t2"));
            fake.EnqueueResult(0, sb.ToString(), "");

            var provider = new GitHistoryProvider(fake);
            var commits = provider.GetRecentCommits(new HistoryOptions(Last: 2, MaxBodyLines: 5), "/repo");
            Assert.Equal(2, commits.Count);
            Assert.Equal("h1", commits[0].Hash);
            Assert.Equal("h2", commits[1].Hash);
        }

        [Fact]
        public void Returns_Empty_When_Git_Fails()
        {
            var fake = new FakeProcessRunner();
            fake.EnqueueResult(exitCode: 128, stdout: "", stderr: "fatal: not a git repository");
            var provider = new GitHistoryProvider(fake);
            var commits = provider.GetRecentCommits(new HistoryOptions(), "/repo");
            Assert.Empty(commits);
        }
    }
}
