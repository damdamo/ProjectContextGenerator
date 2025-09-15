using System.Collections.Generic;
using System.Linq;
using ProjectContextGenerator.Domain.Abstractions;
using ProjectContextGenerator.Domain.Options;
using ProjectContextGenerator.Domain.Rendering;
using ProjectContextGenerator.Domain.Services;
using ProjectContextGenerator.Infrastructure.Filtering;
using ProjectContextGenerator.Infrastructure.GitIgnore;
using ProjectContextGenerator.Infrastructure.Globbing;
using ProjectContextGenerator.Tests.Fakes;
using Xunit;

namespace ProjectContextGenerator.Tests.TreeBuilderTests
{
    public class TreeBuilder_GitIgnore_Nested_Tests
    {
        /// <summary>
        /// Git semantics: you cannot re-include a file if a parent directory is excluded.
        /// With only "!bin/keep/" in the child scope (and "bin/" ignored at the parent),
        /// "src/bin" remains ignored → the "keep" subtree is never traversed or rendered.
        /// </summary>
        [Fact]
        public void Nested_negation_without_parent_unignore_does_not_resurrect()
        {
            // Arrange
            var root = "/repo";

            var fs = new FakeFileSystem(
                directories:
                [
                    "/repo/bin",
                    "/repo/src",
                    "/repo/src/bin",
                    "/repo/src/bin/keep",
                    "/repo/src/bin/other"
                ],
                files:
                [
                    "/repo/.gitignore",
                    "/repo/src/.gitignore",
                    "/repo/src/bin/keep/visible.txt",
                    "/repo/src/bin/other/hidden.txt",
                    "/repo/src/Program.cs"
                ]
            );

            // Parent ignores any "bin/"; child tries to unignore only "bin/keep/" (incorrect per git semantics)
            var parentRules = GitIgnoreParser.Parse("bin/\n");
            var childRules = GitIgnoreParser.Parse("!bin/keep/\n");

            var scoped = new List<(string Scope, GitIgnoreParser.GitIgnoreRule Rule)>();
            scoped.AddRange(parentRules.Select(r => ("", r)));      // root scope
            scoped.AddRange(childRules.Select(r => ("src", r)));    // child scope

            var ignore = new GitIgnoreRuleSet(scoped);

            var include = new GlobPathMatcher(includeGlobs: null, excludeGlobs: null);
            IPathFilter filter = new CompositePathFilter(include, excludeMatcher: null, ignoreRuleSet: ignore);
            var sut = new TreeBuilder(fs, filter);

            // Act
            var tree = sut.Build(root, new TreeScanOptions(MaxDepth: -1));
            var md = new MarkdownTreeRenderer().Render(tree);

            // Assert: root "bin" hidden, and "src/bin/keep" does NOT appear (parent was never unignored)
            Assert.DoesNotContain("\n- bin/\n", md);        // root bin suppressed
            Assert.Contains("- src/", md);                  // src is present
            Assert.DoesNotContain("bin/keep/", md);         // not resurrected
            Assert.DoesNotContain("visible.txt", md);       // not traversed
            Assert.DoesNotContain("bin/other/", md);        // still ignored
            Assert.DoesNotContain("hidden.txt", md);
        }
    }
}