using ProjectContextGenerator.Domain.Abstractions;
using ProjectContextGenerator.Domain.Options;
using ProjectContextGenerator.Domain.Rendering;
using ProjectContextGenerator.Domain.Services;
using ProjectContextGenerator.Infrastructure.Filtering;
using ProjectContextGenerator.Infrastructure.GitIgnore;
using ProjectContextGenerator.Infrastructure.Globbing;
using ProjectContextGenerator.Tests.Fakes;

namespace ProjectContextGenerator.Tests.TreeBuilderTests
{
    public class TreeBuilder_GitIgnore_RootOnly_Tests
    {
        /// <summary>
        /// Verifies that a root .gitignore with directory and file patterns is respected:
        /// - "bin/" hides the directory and its contents.
        /// - "*.log" hides logs, except for an explicit negation "!keep.log".
        /// - Character classes like "[Dd]ebug/" are correctly supported.
        /// </summary>
        [Fact]
        public void Root_gitignore_hides_bin_and_logs_but_keeps_negations()
        {
            // Arrange
            var root = "/repo";

            var fs = new FakeFileSystem(
                directories:
                [
                    "/repo/bin",
                    "/repo/Debug",
                    "/repo/src"
                ],
                files:
                [
                    "/repo/.gitignore",
                    "/repo/bin/hidden.txt",
                    "/repo/normal.log",
                    "/repo/keep.log",
                    "/repo/src/Program.cs"
                ]
            );

            // Build ignore rules directly from in-memory content (root-only)
            var gitignoreText = """
                # Hide build outputs and logs
                [Bb]in/
                [Dd]ebug/
                *.log
                !keep.log
                """;
            var rules = GitIgnoreParser.Parse(gitignoreText);
            var ignore = new GitIgnoreRuleSet(rules);

            // Compose filter: include all by default; no explicit exclude globs here
            var include = new GlobPathMatcher(includeGlobs: null, excludeGlobs: null);
            IPathFilter filter = new CompositePathFilter(includeMatcher: include, excludeMatcher: null, ignoreRuleSet: ignore);

            var sut = new TreeBuilder(fs, filter);

            // Act
            var tree = sut.Build(root, new TreeScanOptions(MaxDepth: -1)); // unlimited depth for clarity
            var md = new MarkdownTreeRenderer().Render(tree);

            // Assert
            Assert.DoesNotContain("bin/", md);                 // directory suppressed by [Bb]in/
            Assert.DoesNotContain("hidden.txt", md);           // file inside bin/ also hidden
            Assert.DoesNotContain("normal.log", md);           // *.log ignored
            Assert.Contains("keep.log", md);                   // negation resurrects this file
            Assert.DoesNotContain("Debug", md);             // [Dd]ebug/ suppressed
            Assert.Contains("Program.cs", md);                 // unrelated file remains
        }
    }
}
