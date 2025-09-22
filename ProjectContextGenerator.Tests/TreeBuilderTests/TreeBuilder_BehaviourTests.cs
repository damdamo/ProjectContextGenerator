using ProjectContextGenerator.Domain.Abstractions;
using ProjectContextGenerator.Domain.Models;
using ProjectContextGenerator.Domain.Options;
using ProjectContextGenerator.Domain.Rendering;
using ProjectContextGenerator.Domain.Services;
using ProjectContextGenerator.Tests.Fakes;
using ProjectContextGenerator.Tests.GlobbingTests;

namespace ProjectContextGenerator.Tests.TreeBuilderTests
{
    public sealed class TreeBuilder_BehaviourTests
    {
        [Fact]
        public void Collapses_single_child_directories()
        {
            var root = "/r";
            var fs = new FakeFileSystem(
                directories: ["/r/A", "/r/A/B", "/r/A/B/C"],
                files: ["/r/A/B/C/file.txt"]
            );
            IPathFilter filter = TestMatchers.Filter();

            var sut = new TreeBuilder(fs, filter);
            var tree = sut.Build(root, new TreeScanOptions(CollapseSingleChildDirectories: true));
            var md = new MarkdownTreeRenderer().Render(tree);

            // Expect "A/B/C/" collapsed into a single name chain somewhere:
            Assert.Contains("- A/B/C/", md);
            Assert.Contains("file.txt", md);
        }

        [Fact]
        public void Sorts_directories_first_and_then_by_name()
        {
            var root = "/r";
            var fs = new FakeFileSystem(
                directories: ["/r/src", "/r/docs"],
                files: ["/r/zzz.txt", "/r/aaa.txt"]
            );
            IPathFilter filter = TestMatchers.Filter();

            var sut = new TreeBuilder(fs, filter);
            var md = new MarkdownTreeRenderer().Render(sut.Build(root, new TreeScanOptions(SortDirectoriesFirst: true)));

            var srcIndex = md.IndexOf("- src/");
            var docsIndex = md.IndexOf("- docs/");
            var aaaIndex = md.IndexOf("aaa.txt");
            var zzzIndex = md.IndexOf("zzz.txt");

            Assert.True(docsIndex < srcIndex, "dirs sorted alphabetically");
            Assert.True(docsIndex < aaaIndex, "dirs come before files");
            Assert.True(aaaIndex < zzzIndex, "files sorted alphabetically");
        }

        [Fact]
        public void Caps_items_per_directory_and_appends_more_marker()
        {
            var root = "/r";
            var fs = new FakeFileSystem(
                directories: [],
                files: Enumerable.Range(1, 10).Select(i => $"/r/file{i}.txt")
            );
            IPathFilter filter = TestMatchers.Filter();

            var sut = new TreeBuilder(fs, filter);
            var md = new MarkdownTreeRenderer().Render(
                sut.Build(root, new TreeScanOptions(MaxItemsPerDirectory: 3))
            );

            Assert.Contains("file1.txt", md);
            Assert.Contains("file2.txt", md);
            Assert.DoesNotContain("file3.txt", md);
            Assert.Contains("… (+7 more)", md);
        }

        [Fact]
        public void DirectoriesOnly_skips_all_files_but_keeps_directory_structure()
        {
            var root = "/r";
            var fs = new FakeFileSystem(
                directories: ["/r/src", "/r/src/Sub", "/r/docs"],
                files: ["/r/src/Program.cs", "/r/src/Sub/Sub.cs", "/r/README.md"]
            );
            IPathFilter filter = TestMatchers.Filter();

            var sut = new TreeBuilder(fs, filter);
            var tree = sut.Build(root, new TreeScanOptions(DirectoriesOnly: true, MaxDepth: -1));

            bool hasFileNode = HasFileNode(tree);
            Assert.False(hasFileNode, "No FileNode should be present when DirectoriesOnly is true.");

            var rootNames = tree.Children.Select(c => c.Name).OrderBy(n => n).ToArray();
            Assert.Contains("docs", rootNames);
            Assert.Contains(rootNames, n => n.StartsWith("src"));

            // helper local
            static bool HasFileNode(DirectoryNode d)
            {
                foreach (var child in d.Children)
                {
                    if (child is FileNode)
                        return true;
                    if (child is DirectoryNode dd && HasFileNode(dd))
                        return true;
                }
                return false;
            }
        
        }
    }
}
