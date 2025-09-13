using ProjectContextGenerator.Domain.Abstractions;
using ProjectContextGenerator.Domain.Options;
using ProjectContextGenerator.Domain.Services;
using ProjectContextGenerator.Domain.Rendering;
using ProjectContextGenerator.Tests.Fakes;
using ProjectContextGenerator.Tests.GlobbingTests;

namespace ProjectContextGenerator.Tests.TreeBuilderTests
{
    public sealed class TreeBuilderFilteringTests
    {
        [Fact]
        public void Excludes_bin_and_obj_directories_even_when_empty()
        {
            // Arrange
            var root = "/repo";
            var fs = new FakeFileSystem(
                directories:
                [
                    "/repo/bin",
                    "/repo/obj",
                    "/repo/src",
                    "/repo/src/Domain",
                    "/repo/src/UI"
                ],
                files:
                [
                    "/repo/src/Domain/Thing.cs",
                    "/repo/src/UI/App.xaml"
                ]
            );

            IPathMatcher matcher = TestMatchers.Matcher(
                excludes: ["**/bin/**", "**/obj/**"]
            );

            var options = new TreeScanOptions(MaxDepth: 10, SortDirectoriesFirst: true);
            var sut = new TreeBuilder(fs, matcher);

            // Act
            var tree = sut.Build(root, options);
            var rendered = new MarkdownTreeRenderer().Render(tree);

            // Assert (no bin/ or obj/)
            Assert.DoesNotContain("bin/", rendered);
            Assert.DoesNotContain("obj/", rendered);
            Assert.Contains("- src/", rendered);
            Assert.Contains("- Domain/", rendered);
            Assert.Contains("Thing.cs", rendered);
        }

        [Fact]
        public void Applies_includes_and_excludes_together()
        {
            var root = "/repo";
            var fs = new FakeFileSystem(
                directories:
                [
                    "/repo/src",
                    "/repo/src/Domain",
                    "/repo/src/Infra",
                ],
                files:
                [
                    "/repo/src/Domain/A.cs",
                    "/repo/src/Infra/B.cs",
                    "/repo/src/Infra/secret.txt"
                ]
            );

            IPathMatcher matcher = TestMatchers.Matcher(
                includes: ["**/*.cs", "**/src/**"],   // only show code files under src
                excludes: ["**/Infra/**"]             // hide Infra entirely
            );

            var sut = new TreeBuilder(fs, matcher);
            var tree = sut.Build(root, new TreeScanOptions(MaxDepth: 10));
            var markdown = new MarkdownTreeRenderer().Render(tree);

            Assert.Contains("A.cs", markdown);
            Assert.DoesNotContain("Infra/", markdown);
            Assert.DoesNotContain("secret.txt", markdown);
        }
    }
}