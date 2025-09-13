using ProjectContextGenerator.Domain.Models;
using ProjectContextGenerator.Domain.Rendering;

namespace ProjectContextGenerator.Tests.RenderingTests
{
    public sealed class MarkdownTreeRendererTests
    {
        [Fact]
        public void Renders_without_bold_and_with_trailing_slash_for_folders()
        {
            var tree = new DirectoryNode("root",
            [
                new DirectoryNode("Abstractions",
                [
                    new FileNode("IFileSystem.cs")
                ]),
                new FileNode("root.csproj")
            ]);

            var md = new MarkdownTreeRenderer().Render(tree);

            Assert.Contains("/root/", md);
            Assert.Contains("- Abstractions/", md);
            Assert.Contains("- IFileSystem.cs", md);
            Assert.DoesNotContain("**", md);
        }
    }
}
