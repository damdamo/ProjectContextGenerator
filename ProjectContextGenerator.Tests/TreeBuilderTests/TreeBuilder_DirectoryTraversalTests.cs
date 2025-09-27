using ProjectContextGenerator.Domain.Abstractions;
using ProjectContextGenerator.Domain.Models;
using ProjectContextGenerator.Domain.Options;
using ProjectContextGenerator.Domain.Rendering;
using ProjectContextGenerator.Domain.Services;
using ProjectContextGenerator.Infrastructure.Filtering;
using ProjectContextGenerator.Infrastructure.Globbing;
using ProjectContextGenerator.Infrastructure.GitIgnore;
using ProjectContextGenerator.Tests.Fakes;
using Xunit;

namespace ProjectContextGenerator.Tests.TreeBuilderTests
{
    public sealed class TreeBuilder_DirectoryTraversalTests
    {
        private static IPathFilter BuildFilter(string[]? includes, string[]? excludes, IIgnoreRuleSet ignore)
        {
            IPathMatcher? includeMatcher = includes is { Length: > 0 } ? new GlobPathMatcher(includes, null) : null;
            IPathMatcher? excludeMatcher = excludes is { Length: > 0 } ? new GlobPathMatcher(new[] { "**/*" }, excludes) : null;
            return new CompositePathFilter(includeMatcher, excludeMatcher, ignore);
        }

        private static TreeScanOptions Opts(
            int maxDepth = -1,
            bool directoriesOnly = false,
            GitIgnoreMode gitIgnore = GitIgnoreMode.None,
            string? gitIgnoreFileName = ".gitignore")
            => new(
                MaxDepth: maxDepth,
                IncludeGlobs: null,
                ExcludeGlobs: null,
                SortDirectoriesFirst: true,
                CollapseSingleChildDirectories: true,
                MaxItemsPerDirectory: null,
                GitIgnore: gitIgnore,
                GitIgnoreFileName: gitIgnoreFileName,
                DirectoriesOnly: directoriesOnly
            );

        [Fact]
        public void Traversal_With_FileOnly_Includes_Produces_Minimal_Directory_Spine()
        {
            // /repo/src/A.cs, /repo/src/sub/B.csproj, /repo/README.md (non-inclus)
            var fs = new FakeFileSystem(
                directories: new[]
                {
                    "/repo/",
                    "/repo/src/",
                    "/repo/src/sub/",
                    "/repo/bin/",
                },
                files: new[]
                {
                    "/repo/src/A.cs",
                    "/repo/src/sub/B.csproj",
                    "/repo/README.md",
                    "/repo/bin/temp.txt"
                }
            );

            var filter = BuildFilter(
                includes: new[] { "**/*.cs", "**/*.csproj" },
                excludes: new[] { "**/bin/**" }, // pas de bin
                ignore: EmptyIgnoreRuleSet.Instance);

            var sut = new TreeBuilder(fs, filter);
            var tree = sut.Build("/repo", Opts(maxDepth: -1, directoriesOnly: false));

            var md = new MarkdownTreeRenderer().Render(tree);

            // Fichiers inclus
            Assert.Contains("A.cs", md);
            Assert.Contains("B.csproj", md);
            // Fichier non inclus
            Assert.DoesNotContain("README.md", md);

            // Structure minimale
            Assert.Contains("src/", md);
            Assert.Contains("sub/", md);
            // bin coupé par exclude
            Assert.DoesNotContain("bin/", md);
            Assert.DoesNotContain("temp.txt", md);
        }

        [Fact]
        public void Exclude_Directory_Blocks_Traversal()
        {
            var fs = new FakeFileSystem(
                directories: new[]
                {
                    "/repo/",
                    "/repo/src/",
                    "/repo/src/bin/",
                    "/repo/src/ok/"
                },
                files: new[]
                {
                    "/repo/src/bin/Hidden.cs",
                    "/repo/src/ok/Visible.cs"
                }
            );

            var filter = BuildFilter(
                includes: new[] { "**/*.cs" },
                excludes: new[] { "**/bin/**" }, // coupe la descente
                ignore: EmptyIgnoreRuleSet.Instance);

            var sut = new TreeBuilder(fs, filter);
            var tree = sut.Build("/repo", Opts());

            var md = new MarkdownTreeRenderer().Render(tree);

            // bin/ absent, pas de fichiers dessous
            Assert.DoesNotContain("bin/", md);
            Assert.DoesNotContain("Hidden.cs", md);

            // ok/ présent avec Visible.cs
            Assert.Contains("ok/", md);
            Assert.Contains("Visible.cs", md);
        }

        [Fact]
        public void Exclude_Files_Does_Not_Block_Traversal_But_Filters_At_Render()
        {
            var fs = new FakeFileSystem(
                directories: new[]
                {
                    "/repo/",
                    "/repo/src/",
                },
                files: new[]
                {
                    "/repo/src/GeneratedFoo.cs",
                    "/repo/src/RealFile.cs"
                }
            );

            var filter = BuildFilter(
                includes: new[] { "**/*.cs" },
                excludes: new[] { "**/Generated*.cs" }, // exclut fichier mais pas le dossier
                ignore: EmptyIgnoreRuleSet.Instance);

            var sut = new TreeBuilder(fs, filter);
            var tree = sut.Build("/repo", Opts());

            var md = new MarkdownTreeRenderer().Render(tree);

            Assert.Contains("src/", md);
            Assert.Contains("RealFile.cs", md);
            Assert.DoesNotContain("GeneratedFoo.cs", md);
        }

        [Fact]
        public void DirectoriesOnly_Traverses_And_Renders_Directories_Only()
        {
            var fs = new FakeFileSystem(
                directories:
                [
                    "/repo/",
                    "/repo/src/",
                    "/repo/src/sub/"
                ],
                files:
                [
                    "/repo/src/sub/B.csproj"
                ]
            );

            var filter = BuildFilter(
                includes: new[] { "**/*.csproj" },
                excludes: null,
                ignore: EmptyIgnoreRuleSet.Instance);

            var sut = new TreeBuilder(fs, filter);
            var tree = sut.Build("/repo", Opts(maxDepth: -1, directoriesOnly: true));

            var md = new MarkdownTreeRenderer().Render(tree);

            // Dossiers présents
            Assert.Contains("src/", md);
            Assert.Contains("sub/", md);
            // Aucun fichier rendu
            Assert.DoesNotContain(".csproj", md);
        }
    }
}