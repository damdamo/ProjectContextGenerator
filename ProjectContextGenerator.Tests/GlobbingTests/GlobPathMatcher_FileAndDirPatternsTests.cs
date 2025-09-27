using ProjectContextGenerator.Infrastructure.Globbing;
using ProjectContextGenerator.Tests.GlobbingTests;
using ProjectContextGenerator.Domain.Abstractions;
using Xunit;

namespace ProjectContextGenerator.Tests.GlobbingTests
{
    public sealed class GlobPathMatcher_FileAndDirPatternsTests
    {
        [Theory]
        [InlineData("src/Program.cs", false, true)]  // file should match
        [InlineData("src/sub/Util.cs", false, true)] // recursive
        [InlineData("src", true, false)]             // directory should NOT match with file-only pattern
        [InlineData("src/sub", true, false)]
        public void Pattern_FilesOnly__DoesNotMatch_Directories(string rel, bool isDir, bool expected)
        {
            IPathMatcher m = new GlobPathMatcher(new[] { "**/*.cs" }, null);
            Assert.Equal(expected, m.IsMatch(rel.Replace('\\', '/'), isDir));
        }

        [Theory]
        [InlineData("bin", true, true)]
        [InlineData("bin/", true, true)]
        [InlineData("bin/obj", true, true)]
        [InlineData("bin/any/file.txt", false, true)]
        [InlineData("src/bin/file.txt", false, true)]
        public void Pattern_DirectorySubtree__Matches_All_Under_It(string rel, bool isDir, bool expected)
        {
            IPathMatcher m = new GlobPathMatcher(new[] { "**/bin/**" }, null);
            Assert.Equal(expected, m.IsMatch(rel.Replace('\\', '/'), isDir));
        }

        [Theory]
        [InlineData("src/Program.cs", false, true)]
        [InlineData("src/Project.csproj", false, true)]
        [InlineData("src/README.md", false, false)]
        public void Pattern_MultipleExtensions_Using_TwoPatterns(string rel, bool isDir, bool expected)
        {
            // (Si ta lib ne supporte pas {cs,csproj}, on combine deux patterns)
            IPathMatcher m = new GlobPathMatcher(new[] { "**/*.cs", "**/*.csproj" }, null);
            Assert.Equal(expected, m.IsMatch(rel, isDir));
        }

        [Fact]
        public void Include_Then_Exclude__Exclude_Wins()
        {
            var include = new GlobPathMatcher(new[] { "**/*.cs" }, null);
            var exclude = new GlobPathMatcher(new[] { "**/*" }, new[] { "**/Generated*.cs" });

            // Simule la décision CompositePathFilter : include -> (gitignore) -> exclude
            var p1 = "src/GeneratedFoo.cs";
            var p2 = "src/RealFile.cs";

            Assert.True(include.IsMatch(p1, false));
            Assert.True(include.IsMatch(p2, false));

            // exclude-matcher returns true when NOT excluded; here it should be false for Generated*.cs
            Assert.False(exclude.IsMatch(p1, false)); // excluded
            Assert.True(exclude.IsMatch(p2, false));  // kept
        }

        [Fact]
        public void No_Includes__Means_Include_All()
        {
            IPathMatcher m = new GlobPathMatcher(null, null);
            Assert.True(m.IsMatch("any/path/file.cs", false));
            Assert.True(m.IsMatch("any/folder", true));
        }
    }
}