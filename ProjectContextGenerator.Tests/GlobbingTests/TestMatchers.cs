using ProjectContextGenerator.Domain.Abstractions;
using ProjectContextGenerator.Infrastructure.Filtering;
using ProjectContextGenerator.Infrastructure.Globbing;
using ProjectContextGenerator.Infrastructure.GitIgnore;

namespace ProjectContextGenerator.Tests.GlobbingTests
{
    public static class TestMatchers
    {
        public static IPathMatcher Matcher(string[]? includes = null, string[]? excludes = null)
            => new GlobPathMatcher(includes, excludes);

        /// <summary>
        /// Builds a default <see cref="IPathFilter"/> for tests that only care about globbing.
        /// Ignores .gitignore rules.
        /// </summary>
        public static IPathFilter Filter(string[]? includes = null, string[]? excludes = null)
        {
            var includeMatcher = new GlobPathMatcher(includes, null);
            var excludeMatcher = excludes is { Length: > 0 }
                ? new GlobPathMatcher(["**/*"], excludes)
                : null;

            return new CompositePathFilter(includeMatcher, excludeMatcher, EmptyIgnoreRuleSet.Instance);
        }
    }
}