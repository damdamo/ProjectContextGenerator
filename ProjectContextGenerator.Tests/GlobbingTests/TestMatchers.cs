using ProjectContextGenerator.Domain.Abstractions;
using ProjectContextGenerator.Infrastructure;

namespace ProjectContextGenerator.Tests.GlobbingTests
{
    public static class TestMatchers
    {
        public static IPathMatcher Matcher(string[]? includes = null, string[]? excludes = null)
            => new GlobPathMatcher(includes, excludes);
    }
}