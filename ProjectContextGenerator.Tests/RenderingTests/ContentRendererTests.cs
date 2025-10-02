using System.Linq;
using ProjectContextGenerator.Domain.Models;
using ProjectContextGenerator.Domain.Options;
using ProjectContextGenerator.Domain.Rendering;
using ProjectContextGenerator.Tests.Fakes;
using Xunit;

using ProjectContextGenerator.Infrastructure.Globbing; // GlobPathMatcher
using ProjectContextGenerator.Domain.Abstractions;     // IPathMatcher

namespace ProjectContextGenerator.Tests.RenderingTests
{
    public class ContentRendererTests
    {
        // Helper to build a renderer without content include (no content expected)
        private static (FakeFileSystem fs, MarkdownTreeRenderer renderer, FileNode file, DirectoryNode root)
            Setup_NoContentInclude(string content, ContentOptions opts)
        {
            var fs = new FakeFileSystem(
                directories: ["/repo/"],
                files: ["/repo/Foo.cs"]
            );
            fs.SetFileContent("/repo/Foo.cs", content);

            var file = new FileNode("Foo.cs", "Foo.cs");
            var root = new DirectoryNode("repo", "", [file]);

            // No include matcher → even if Enabled=true, no content is rendered
            var renderer = new MarkdownTreeRenderer(fs, "/repo", opts /*, contentIncludeMatcher: null (default)*/);
            return (fs, renderer, file, root);
        }

        // Helper to build a renderer with content include matching Foo.cs (content expected)
        private static (FakeFileSystem fs, MarkdownTreeRenderer renderer, FileNode file, DirectoryNode root)
            Setup_WithContentInclude(string content, ContentOptions opts, params string[] includeGlobs)
        {
            var fs = new FakeFileSystem(
                directories: ["/repo/"],
                files: ["/repo/Foo.cs"]
            );
            fs.SetFileContent("/repo/Foo.cs", content);

            var file = new FileNode("Foo.cs", "Foo.cs");
            var root = new DirectoryNode("repo", "", [file]);

            IPathMatcher contentMatcher = new GlobPathMatcher(
                includeGlobs is { Length: > 0 } ? includeGlobs : ["Foo.cs"],
                excludeGlobs: null
            );

            var renderer = new MarkdownTreeRenderer(fs, "/repo", opts, contentMatcher);
            return (fs, renderer, file, root);
        }

        [Fact]
        public void DisabledContent_ShouldRenderOnlyTree()
        {
            var opts = new ContentOptions(Enabled: false);
            var (_, renderer, _, root) = Setup_NoContentInclude("class Foo {}", opts);

            var output = renderer.Render(root);

            Assert.Contains("- Foo.cs", output);
            Assert.DoesNotContain("class Foo", output);
        }

        [Fact]
        public void IndentDepth0_ShouldKeepTopLevelOnly()
        {
            var code = "namespace X\n{\n    class Foo {}\n}";
            // IMPORTANT: set ContextPadding = 0 so that indented lines are not reintroduced by padding
            var opts = new ContentOptions(
                Enabled: true,
                IndentDepth: 0,
                TabWidth: 4,
                DetectTabWidth: false,
                ContextPadding: 0,
                Include: ["Foo.cs"]
            );

            var (_, renderer, _, root) = Setup_WithContentInclude(code, opts, "Foo.cs");
            var output = renderer.Render(root);

            Assert.Contains("namespace X", output);
            Assert.DoesNotContain("class Foo", output);
        }

        [Fact]
        public void IndentDepth1_ShouldIncludeOneMoreLevel()
        {
            var code = "namespace X\n{\n    class Foo {}\n}";
            var opts = new ContentOptions(
                Enabled: true,
                IndentDepth: 1,
                TabWidth: 4,
                DetectTabWidth: false,
                Include: ["Foo.cs"]
            );

            var (_, renderer, _, root) = Setup_WithContentInclude(code, opts, "Foo.cs");
            var output = renderer.Render(root);

            Assert.Contains("namespace X", output);
            Assert.Contains("class Foo", output);
        }

        [Fact]
        public void MaxLinesPerFile_ShouldTruncateOutput()
        {
            var code = string.Join("\n", Enumerable.Range(1, 50).Select(i => $"Line {i}"));
            var opts = new ContentOptions(
                Enabled: true,
                IndentDepth: -1,
                MaxLinesPerFile: 10,
                Include: ["Foo.cs"]
            );

            var (_, renderer, _, root) = Setup_WithContentInclude(code, opts, "Foo.cs");
            var output = renderer.Render(root);

            Assert.Contains("Line 1", output);
            Assert.Contains("Line 10", output);
            Assert.Contains("... (truncated", output);
            Assert.DoesNotContain("Line 50", output);
        }

        [Fact]
        public void ContextPadding_ShouldKeepNeighbors()
        {
            var code = "root\n    child1\n    child2\n    child3";
            var opts = new ContentOptions(
                Enabled: true,
                IndentDepth: 0,
                ContextPadding: 1,
                Include: ["Foo.cs"]
            );

            var (_, renderer, _, root) = Setup_WithContentInclude(code, opts, "Foo.cs");
            var output = renderer.Render(root);

            Assert.Contains("root", output);
            // Padding should keep at least one indented child line
            Assert.Contains("child1", output);
        }

        [Fact]
        public void ShowLineNumbers_ShouldPrefixLines()
        {
            var code = "lineA\nlineB\nlineC";
            var opts = new ContentOptions(
                Enabled: true,
                IndentDepth: -1,
                ShowLineNumbers: true,
                Include: ["Foo.cs"]
            );

            var (_, renderer, _, root) = Setup_WithContentInclude(code, opts, "Foo.cs");
            var output = renderer.Render(root);

            Assert.Contains("1:", output);
            Assert.Contains("2:", output);
            Assert.Contains("3:", output);
        }

        [Fact]
        public void EnabledButNoInclude_ShouldRenderNoContent()
        {
            var code = "class Foo {}";
            var opts = new ContentOptions(
                Enabled: true,
                IndentDepth: -1
            // Include intentionally omitted or empty → no content expected
            );

            var (_, renderer, _, root) = Setup_NoContentInclude(code, opts);
            var output = renderer.Render(root);

            Assert.Contains("- Foo.cs", output);
            Assert.DoesNotContain("class Foo", output);
        }

        [Fact]
        public void MaxFiles_ShouldCapRenderedFiles()
        {
            // Arrange: repository with two files to demonstrate the cap
            var fs = new FakeFileSystem(
                directories: ["/repo/"],
                files: ["/repo/A.cs", "/repo/B.cs"]
            );
            fs.SetFileContent("/repo/A.cs", "class A {}");
            fs.SetFileContent("/repo/B.cs", "class B {}");

            var a = new FileNode("A.cs", "A.cs");
            var b = new FileNode("B.cs", "B.cs");
            var root = new DirectoryNode("repo", "", [a, b]);

            var opts = new ContentOptions(
                Enabled: true,
                IndentDepth: -1,
                MaxFiles: 1,
                Include: ["*.cs"]
            );

            IPathMatcher includeMatcher = new GlobPathMatcher(["*.cs"], excludeGlobs: null);
            var renderer = new MarkdownTreeRenderer(fs, "/repo", opts, includeMatcher);

            // Act
            var output = renderer.Render(root);

            // Assert: both file names present, but content should appear for only one of them
            Assert.Contains("- A.cs", output);
            Assert.Contains("- B.cs", output);

            var hasA = output.Contains("class A");
            var hasB = output.Contains("class B");
            Assert.True(hasA ^ hasB, "Only one file's content should be rendered due to MaxFiles=1.");
        }

        [Fact]
        public void IncludePattern_NoMatch_ShouldRenderNoContent()
        {
            var code = "class Foo {}";
            var opts = new ContentOptions(
                Enabled: true,
                IndentDepth: -1,
                Include: ["*.md"] // ne matche pas Foo.cs
            );

            var (_, renderer, _, root) = Setup_WithContentInclude(code, opts, "*.md");
            var output = renderer.Render(root);

            Assert.Contains("- Foo.cs", output);
            Assert.DoesNotContain("class Foo", output);
        }

        [Fact]
        public void IncludePatterns_WithSubfolder_ShouldRespectNormalizedPaths()
        {
            // Arrange: repo avec /repo/src/Foo.cs
            var fs = new FakeFileSystem(
                directories: ["/repo/", "/repo/src/"],
                files: ["/repo/src/Foo.cs"]
            );
            fs.SetFileContent("/repo/src/Foo.cs", "namespace X { class Foo {} }");

            var file = new FileNode("Foo.cs", "src/Foo.cs");
            var root = new DirectoryNode("repo", "", [file]);

            var optsNoMatch = new ContentOptions(
                Enabled: true,
                IndentDepth: -1,
                Include: ["src/**/I*.cs"]
            );
            IPathMatcher mNo = new GlobPathMatcher(["src/**/I*.cs"], excludeGlobs: null);
            var rNo = new MarkdownTreeRenderer(fs, "/repo", optsNoMatch, mNo);

            var outNo = rNo.Render(root);
            Assert.Contains("- Foo.cs", outNo);
            Assert.DoesNotContain("class Foo", outNo);

            var optsYes = new ContentOptions(
                Enabled: true,
                IndentDepth: -1,
                Include: ["src/**/*.cs"]
            );
            IPathMatcher mYes = new GlobPathMatcher(new[] { "src/**/*.cs" }, excludeGlobs: null);
            var rYes = new MarkdownTreeRenderer(fs, "/repo", optsYes, mYes);

            var outYes = rYes.Render(root);
            Assert.Contains("- Foo.cs", outYes);
            Assert.Contains("class Foo", outYes);
        }

        [Fact]
        public void MaxFiles_Zero_ShouldRenderNoContent()
        {
            var code = "class Foo {}";
            var opts = new ContentOptions(
                Enabled: true,
                IndentDepth: -1,
                MaxFiles: 0,
                Include: ["Foo.cs"]
            );

            IPathMatcher matcher = new GlobPathMatcher(["Foo.cs"], excludeGlobs: null);
            var (_, _, _, root) = Setup_WithContentInclude(code, opts, "Foo.cs");

            var fs = new FakeFileSystem(
                directories: ["/repo/"],
                files: ["/repo/Foo.cs"]
            );
            fs.SetFileContent("/repo/Foo.cs", code);
            var r = new MarkdownTreeRenderer(fs, "/repo", opts, matcher);

            var output = r.Render(root);

            Assert.Contains("- Foo.cs", output);
            Assert.DoesNotContain("class Foo", output); // aucun contenu car MaxFiles=0
        }

        [Fact]
        public void EnabledWithInclude_ButNoMatcher_ShouldRenderNoContent()
        {
            var code = "class Foo {}";
            var opts = new ContentOptions(
                Enabled: true,
                IndentDepth: -1,
                Include: ["Foo.cs"]
            );

            var (_, renderer, _, root) = Setup_NoContentInclude(code, opts);
            var output = renderer.Render(root);

            Assert.Contains("- Foo.cs", output);
            Assert.DoesNotContain("class Foo", output);
        }

    }
}