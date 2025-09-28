using ProjectContextGenerator.Domain.Models;
using ProjectContextGenerator.Domain.Options;
using ProjectContextGenerator.Domain.Rendering;

namespace ProjectContextGenerator.Tests.RenderingTests
{
    public sealed class HistoryRendererTests
    {
        private static string N(string s) => s.Replace("\r\n", "\n");

        private static IReadOnlyList<CommitInfo> SampleCommits() =>
        [
            new CommitInfo
            {
                Hash = "a",
                AuthorName = "alice",
                AuthorEmail = "a@ex",
                DateIso = "2025-09-26T10:00:0002:00",
                Title = "feat: add X",
                BodyLines = []
            },
            new CommitInfo
            {
                Hash = "b",
                AuthorName = "bob",
                AuthorEmail = "b@ex",
                DateIso = "2025-09-26T11:00:0002:00",
                Title = "fix: bug Y",
                BodyLines = ["details 1", "details 2"]
            }
        ];

        [Fact]
        public void Markdown_TitlesOnly_Renders_List()
        {
            var commits = SampleCommits();
            var opts = new HistoryOptions(Last: 20, MaxBodyLines: 6, Detail: HistoryDetail.TitlesOnly);
            var r = new MarkdownHistoryRenderer();
            var s = r.Render(commits, opts);
            var expectedStart = "## Recent Changes (last 20)\n- feat: add X\n- fix: bug Y";
            Assert.StartsWith(N(expectedStart), N(s));
        }

        [Fact]
        public void Markdown_TitleAndBody_Renders_Indented_Body()
        {
            var commits = SampleCommits();
            var opts = new HistoryOptions(Last: 5, MaxBodyLines: 6, Detail: HistoryDetail.TitleAndBody);
            var r = new MarkdownHistoryRenderer();
            var s = r.Render(commits, opts);
            var expectedOutput = "- fix: bug Y\n  details 1\n  details 2";
            Assert.Contains(N(expectedOutput), N(s));
        }

        [Fact]
        public void Markdown_EmptyList_Returns_Empty_String()
        {
            var r = new MarkdownHistoryRenderer();
            var s = r.Render([], new HistoryOptions());
            Assert.Equal(string.Empty, s);
        }

        [Fact]
        public void PlainText_TitlesOnly_Renders_List()
        {
            var commits = SampleCommits();
            var opts = new HistoryOptions(Last: 10, Detail: HistoryDetail.TitlesOnly);
            var r = new PlainTextHistoryRenderer();
            var s = r.Render(commits, opts);
            var expectedStart = "Recent Changes (last 10)\n- feat: add X\n- fix: bug Y";
            Assert.StartsWith(N(expectedStart), N(s));
        }

        [Fact]
        public void PlainText_TitleAndBody_Renders_Indented_Body()
        {
            var commits = SampleCommits();
            var opts = new HistoryOptions(Last: 10, Detail: HistoryDetail.TitleAndBody);
            var r = new PlainTextHistoryRenderer();
            var s = r.Render(commits, opts);
            var expectedOutput = "- fix: bug Y\n  details 1\n  details 2";
            Assert.Contains(N(expectedOutput), N(s));
        }

        [Fact]
        public void PlainText_EmptyList_Returns_Empty_String()
        {
            var r = new PlainTextHistoryRenderer();
            var s = r.Render([], new HistoryOptions());
            Assert.Equal(string.Empty, s);
        }
    }
}
