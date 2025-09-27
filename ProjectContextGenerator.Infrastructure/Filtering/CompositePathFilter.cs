using ProjectContextGenerator.Domain.Abstractions;

namespace ProjectContextGenerator.Infrastructure.Filtering

{
    /// <summary>
    /// High-level policy that composes include globs, exclude globs and .gitignore rules
    /// into a single decision point consumable by the <see cref="ITreeBuilder"/>.
    /// A path is included only if all three checks pass.
    /// </summary>
    /// <remarks>
    /// Creates a new composite filter.
    /// </remarks>
    /// <param name="includeMatcher">Glob-based include matcher; null means "include all".</param>
    /// <param name="excludeMatcher">Glob-based exclude matcher; null means "no excludes".</param>
    /// <param name="ignoreRuleSet">Compiled .gitignore rule set; use an empty set if unsupported.</param>
    public sealed class CompositePathFilter(IPathMatcher? includeMatcher, IPathMatcher? excludeMatcher, IIgnoreRuleSet ignoreRuleSet) : IPathFilter
    {
        private readonly IPathMatcher? _include = includeMatcher;
        private readonly IPathMatcher? _exclude = excludeMatcher;
        private readonly IIgnoreRuleSet _ignore = ignoreRuleSet;

        /// <inheritdoc />
        public bool ShouldIncludeDirectory(string relativePath)
        {
            var p = Normalize(relativePath);
            // 1) Include globs (default: include all if not provided)
            if (_include is not null && !_include.IsMatch(p, isDirectory: true))
                return false;
            // 2) .gitignore rules
            if (_ignore.IsIgnored(p, isDirectory: true))
                return false;
            // 3) Exclude globs
            if (_exclude is not null && !_exclude.IsMatch(p, isDirectory: true))
                return false;
            return true;
        }

        /// <inheritdoc />
        public bool ShouldIncludeFile(string relativePath)
        {
            var p = Normalize(relativePath);
            // 1) Include globs (default: include all if not provided)
            if (_include is not null && !_include.IsMatch(p, isDirectory: false))
                return false;
            // 2) .gitignore rules
            if (_ignore.IsIgnored(p, isDirectory: false))
                return false;
            // 3) Exclude globs
            if (_exclude is not null && !_exclude.IsMatch(p, isDirectory: false))
                return false;
            return true;
        }

        /// <inheritdoc />
        public bool CanTraverseDirectory(string relativePath)
        {
            var p = Normalize(relativePath);
            if (_ignore.IsIgnored(p, isDirectory: true))
                return false;
            if (_exclude is not null && !_exclude.IsMatch(p, isDirectory: true))
                return false;
            return true;
        }

        private static string Normalize(string relativePath)
        {
            // Normalize to forward slashes and strip an optional leading "./"
            var p = relativePath.Replace('\\', '/');
            if (p.StartsWith("./"))
                p = p[2..];
            return p;
        }
    }
}
