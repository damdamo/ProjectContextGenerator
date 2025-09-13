using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using ProjectContextGenerator.Domain.Abstractions;

namespace ProjectContextGenerator.Infrastructure.GitIgnore
{
    /// <summary>
    /// A compiled, immutable set of .gitignore rules that can evaluate
    /// whether a given relative path should be ignored (last rule wins).
    /// Supports:
    ///  - Order-sensitive evaluation (last rule wins).
    ///  - Negation via '!' to re-include paths.
    ///  - Directory-only rules (pattern ending with '/').
    ///  - Root-anchored rules (pattern starting with '/').
    ///  - Glob tokens: '**', '*', '?'.
    ///  - Character classes: '[abc]', ranges like '[a-z]', and negation '[!abc]'.
    ///  - Scoped rules from nested .gitignore files (scope = directory of the file).
    /// </summary>
    public sealed class GitIgnoreRuleSet : IIgnoreRuleSet
    {
        private readonly struct CompiledRule(string scope, bool isNegation, bool isDirectoryOnly, Regex regex)
        {
            public readonly string Scope = scope;          // "" for root; otherwise "dir/subdir" (no trailing '/')
            public readonly bool IsNegation = isNegation;
            public readonly bool IsDirectoryOnly = isDirectoryOnly;
            public readonly Regex Regex = regex;
        }

        private readonly IReadOnlyList<CompiledRule> _compiled;

        /// <summary>
        /// Root-only constructor (kept for backward compatibility).
        /// Treats all rules as coming from the repository root.
        /// </summary>
        public GitIgnoreRuleSet(IReadOnlyList<GitIgnoreParser.GitIgnoreRule> rules)
        {
            if (rules is null || rules.Count == 0)
            {
                _compiled = Array.Empty<CompiledRule>();
                return;
            }

            var list = new List<CompiledRule>(rules.Count);
            foreach (var r in rules)
            {
                var regex = BuildRegex(r.NormalizedPattern, r.IsAnchored, r.IsDirectoryOnly);
                list.Add(new CompiledRule(scope: "", r.IsNegation, r.IsDirectoryOnly, regex));
            }
            _compiled = list;
        }

        /// <summary>
        /// Nested constructor: accepts rules with their directory scopes.
        /// <paramref name="scopedRules"/> must be ordered from parent directories to deeper ones,
        /// and within each .gitignore in file order. (Deeper rules override parent rules.)
        /// </summary>
        public GitIgnoreRuleSet(IReadOnlyList<(string Scope, GitIgnoreParser.GitIgnoreRule Rule)> scopedRules)
        {
            if (scopedRules is null || scopedRules.Count == 0)
            {
                _compiled = [];
                return;
            }

            var list = new List<CompiledRule>(scopedRules.Count);
            foreach (var (scope, r) in scopedRules)
            {
                var regex = BuildRegex(r.NormalizedPattern, r.IsAnchored, r.IsDirectoryOnly);
                list.Add(new CompiledRule(NormScope(scope), r.IsNegation, r.IsDirectoryOnly, regex));
            }
            _compiled = list;
        }

        /// <summary>
        /// Returns true if the given path is ignored by this rule set.
        /// </summary>
        /// <remarks>
        /// .gitignore semantics are order-sensitive: the <b>last matching rule wins</b>.
        /// We iterate compiled rules in reverse order and return on the first match we encounter:
        ///   - If that rule is a negation ('!'), the path is <b>not</b> ignored.
        ///   - Otherwise, the path <b>is</b> ignored.
        /// Directory-only rules (patterns ending with '/') apply only to directories.
        /// For nested .gitignore files, each rule has a <i>scope</i> (the directory that contains
        /// the .gitignore). A rule applies only if the evaluated path is within that scope. We
        /// then evaluate the pattern against the path with the scope prefix removed, so that
        /// anchored ('/') patterns are relative to that scope as in git.
        /// </remarks>
        public bool IsIgnored(string relativePath, bool isDirectory)
        {
            if (string.IsNullOrEmpty(relativePath) || _compiled.Count == 0)
                return false;

            var path = Normalize(relativePath);

            // Iterate from last to first because the last matching rule wins.
            for (int i = _compiled.Count - 1; i >= 0; i--)
            {
                var rule = _compiled[i];

                // Skip directory-only rules when evaluating a file.
                if (rule.IsDirectoryOnly && !isDirectory)
                    continue;

                // Check scope: rule applies only to paths inside its scope.
                // Scope "" means repository root (applies to all paths).
                if (!IsInScope(path, rule.Scope))
                    continue;

                var subPath = StripScope(path, rule.Scope);

                // First match (from the end) determines the decision.
                if (rule.Regex.IsMatch(subPath))
                    return !rule.IsNegation; // true => ignore; false => unignore
            }

            // No rule matched → not ignored.
            return false;
        }

        private static string Normalize(string p)
        {
            p = p.Replace('\\', '/');
            if (p.StartsWith("./", StringComparison.Ordinal))
                p = p[2..];
            if (p.Length > 1 && p.EndsWith("/", StringComparison.Ordinal))
                p = p.TrimEnd('/');
            return p;
        }

        private static string NormScope(string scope)
        {
            if (string.IsNullOrEmpty(scope)) return "";
            var s = scope.Replace('\\', '/').Trim('/');
            return s;
        }

        private static bool IsInScope(string path, string scope)
        {
            if (scope.Length == 0) return true; // root scope applies to all
            if (path.Equals(scope, StringComparison.OrdinalIgnoreCase)) return true;
            if (path.Length > scope.Length + 1 &&
                path.StartsWith(scope, StringComparison.OrdinalIgnoreCase) &&
                path[scope.Length] == '/')
            {
                return true;
            }
            return false;
        }

        private static string StripScope(string path, string scope)
        {
            if (scope.Length == 0) return path;
            if (path.Equals(scope, StringComparison.OrdinalIgnoreCase)) return ""; // scope dir itself
            // Remove "scope/" prefix
            return path[(scope.Length + 1)..];
        }

        /// <summary>
        /// Builds a compiled regex from a gitignore-like pattern.
        /// Handles **, *, ?, and character classes [..] including negation [!..].
        /// </summary>
        private static Regex BuildRegex(string pattern, bool anchored, bool dirOnly)
        {
            var sb = new StringBuilder();

            // Anchored: start at beginning of the (scope-relative) path; else from any segment boundary.
            if (anchored)
                sb.Append('^');
            else
                sb.Append("(?:^|.*/)");

            AppendPatternAsRegex(sb, pattern);

            // Directory-only rules: match the directory itself or anything under it.
            if (dirOnly)
                sb.Append("(?:/.*)?$");
            else
                sb.Append('$');

            return new Regex(sb.ToString(), RegexOptions.Compiled | RegexOptions.CultureInvariant);
        }

        /// <summary>
        /// Translates a glob pattern to regex. Supports '**', '*', '?', and character classes.
        /// </summary>
        private static void AppendPatternAsRegex(StringBuilder sb, ReadOnlySpan<char> pat)
        {
            bool inClass = false;
            for (int i = 0; i < pat.Length; i++)
            {
                char c = pat[i];

                if (!inClass)
                {
                    switch (c)
                    {
                        case '*':
                            if (i + 1 < pat.Length && pat[i + 1] == '*')
                            {
                                sb.Append(".*");
                                i++;
                            }
                            else
                            {
                                sb.Append("[^/]*");
                            }
                            break;

                        case '?':
                            sb.Append("[^/]");
                            break;

                        case '[':
                            inClass = true;
                            sb.Append('[');
                            if (i + 1 < pat.Length && (pat[i + 1] == '!' || pat[i + 1] == '^'))
                            {
                                sb.Append('^');
                                i++;
                            }
                            break;

                        case '.':
                        case '+':
                        case '(':
                        case ')':
                        case '{':
                        case '}':
                        case '|':
                        case '^':
                        case '$':
                            sb.Append('\\').Append(c);
                            break;

                        case '/':
                            sb.Append('/');
                            break;

                        default:
                            sb.Append(c);
                            break;
                    }
                }
                else
                {
                    if (c == ']')
                    {
                        inClass = false;
                        sb.Append(']');
                    }
                    else
                    {
                        if (c == '\\')
                            sb.Append(@"\\");
                        else
                            sb.Append(c);
                    }
                }
            }

            if (inClass)
            {
                sb.Append(@"\[");
            }
        }
    }

    /// <summary>
    /// An empty rule set that never ignores anything.
    /// Useful as a safe default when no .gitignore is found or .gitignore support is disabled.
    /// </summary>
    public sealed class EmptyIgnoreRuleSet : IIgnoreRuleSet
    {
        public static readonly EmptyIgnoreRuleSet Instance = new();

        private EmptyIgnoreRuleSet() { }

        /// <inheritdoc />
        public bool IsIgnored(string relativePath, bool isDirectory) => false;
    }
}
