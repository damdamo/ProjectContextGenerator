using System.Linq;
using ProjectContextGenerator.Domain.Models;
using ProjectContextGenerator.Domain.Options;
using ProjectContextGenerator.Domain.Rendering;
using ProjectContextGenerator.Tests.Fakes;
using Xunit;

namespace ProjectContextGenerator.Tests.RenderingTests
{
    public class ContentRendererTests
    {
        private static (FakeFileSystem fs, MarkdownTreeRenderer renderer, FileNode file, DirectoryNode root) Setup(string content, ContentOptions opts)
        {
            var fs = new FakeFileSystem(
                directories: new[] { "/repo/" },
                files: new[] { "/repo/Foo.cs" }
            );
            fs.SetFileContent("/repo/Foo.cs", content);

            var file = new FileNode("Foo.cs", "Foo.cs");
            var root = new DirectoryNode("repo", "", new[] { file });

            var renderer = new MarkdownTreeRenderer(fs, "/repo", opts);
            return (fs, renderer, file, root);
        }

        [Fact]
        public void DisabledContent_ShouldRenderOnlyTree()
        {
            var opts = new ContentOptions(Enabled: false);
            var (_, renderer, _, root) = Setup("class Foo {}", opts);

            var output = renderer.Render(root);

            Assert.Contains("- Foo.cs", output);
            Assert.DoesNotContain("class Foo", output);
        }

        [Fact]
        public void IndentDepth0_ShouldKeepTopLevelOnly()
        {
            var code = "namespace X\n{\n    class Foo {}\n}";
            // IMPORTANT: set ContextPadding = 0 so that indented lines are not reintroduced by padding
            var opts = new ContentOptions(Enabled: true, IndentDepth: 0, TabWidth: 4, DetectTabWidth: false, ContextPadding: 0);

            var (_, renderer, _, root) = Setup(code, opts);
            var output = renderer.Render(root);

            Assert.Contains("namespace X", output);
            Assert.DoesNotContain("class Foo", output);
        }

        [Fact]
        public void IndentDepth1_ShouldIncludeOneMoreLevel()
        {
            var code = "namespace X\n{\n    class Foo {}\n}";
            var opts = new ContentOptions(Enabled: true, IndentDepth: 1, TabWidth: 4, DetectTabWidth: false);

            var (_, renderer, _, root) = Setup(code, opts);
            var output = renderer.Render(root);

            Assert.Contains("namespace X", output);
            Assert.Contains("class Foo", output);
        }

        [Fact]
        public void MaxLinesPerFile_ShouldTruncateOutput()
        {
            var code = string.Join("\n", Enumerable.Range(1, 50).Select(i => $"Line {i}"));
            var opts = new ContentOptions(Enabled: true, IndentDepth: -1, MaxLinesPerFile: 10);

            var (_, renderer, _, root) = Setup(code, opts);
            var output = renderer.Render(root);

            Assert.Contains("Line 1", output);
            Assert.Contains("Line 10", output);
            Assert.Contains("… (truncated", output);
            Assert.DoesNotContain("Line 50", output);
        }

        [Fact]
        public void ContextPadding_ShouldKeepNeighbors()
        {
            var code = "root\n    child1\n    child2\n    child3";
            var opts = new ContentOptions(Enabled: true, IndentDepth: 0, ContextPadding: 1);

            var (_, renderer, _, root) = Setup(code, opts);
            var output = renderer.Render(root);

            Assert.Contains("root", output);
            // Padding should keep at least one indented child line
            Assert.Contains("child1", output);
        }

        [Fact]
        public void ShowLineNumbers_ShouldPrefixLines()
        {
            var code = "lineA\nlineB\nlineC";
            var opts = new ContentOptions(Enabled: true, IndentDepth: -1, ShowLineNumbers: true);

            var (_, renderer, _, root) = Setup(code, opts);
            var output = renderer.Render(root);

            Assert.Contains("1:", output);
            Assert.Contains("2:", output);
            Assert.Contains("3:", output);
        }
    }
}