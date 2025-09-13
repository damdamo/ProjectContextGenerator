using ProjectContextGenerator.Domain.Abstractions;
using ProjectContextGenerator.Domain.Options;
using ProjectContextGenerator.Domain.Rendering;
using ProjectContextGenerator.Domain.Services;
using ProjectContextGenerator.Tests.Fakes;

namespace ProjectContextGenerator.Tests.GlobbingTests
{
    public sealed class GlobPathMatcherTests
    {
        [Theory]
        [InlineData("src/obj", true)]
        [InlineData("src/obj/", true)]
        [InlineData("src/bin", true)]
        [InlineData("src/bin/any", true)]
        [InlineData("src/domain", false)]
        public void Excludes_common_build_folders(string rel, bool shouldExclude)
        {
            var m = TestMatchers.Matcher(excludes: ["**/obj/**", "**/bin/**"]);
            var isDir = !rel.Contains('.');
            var result = m.IsMatch(rel, isDir);
            Assert.Equal(!shouldExclude, result);
        }

        [Fact]
        public void Excludes_by_extension()
        {
            // Arrange: virtual file system with both .cs and .csproj files
            var fs = new FakeFileSystem(
                directories: ["/repo/Abstractions"],
                files:
                [
                    "/repo/Abstractions/IFileSystem.cs",
                    "/repo/ProjectContextGenerator.Domain.csproj"
                ]
            );

            // Exclude all .cs files
            IPathMatcher matcher = TestMatchers.Matcher(excludes: ["**/*.cs"]);

            var sut = new TreeBuilder(fs, matcher);

            // Act: build tree
            var tree = sut.Build("/repo", new TreeScanOptions());
            var md = new MarkdownTreeRenderer().Render(tree);

            // Assert: .cs files excluded, .csproj included
            Assert.DoesNotContain("IFileSystem.cs", md);
            Assert.Contains("ProjectContextGenerator.Domain.csproj", md);
        }

        [Fact]
        public void Includes_default_everything_when_no_includes_specified()
        {
            var m = TestMatchers.Matcher();
            Assert.True(m.IsMatch("any/path/file.cs", false));
            Assert.True(m.IsMatch("any/folder", true));
        }
    }
}
