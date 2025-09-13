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
    ///  - Glob tokens: '*', '?', '**' (where ** can cross directory separators).
    ///  - Character classes: '[abc]', ranges like '[a-z]', and negation '[!abc]'.
    /// </summary>
    public sealed class GitIgnoreRuleSet : IIgnoreRuleSet
    {
        private readonly struct CompiledRule(bool isNegation, bool isDirectoryOnly, Regex regex)
        {
            public readonly bool IsNegation = isNegation;
            public readonly bool IsDirectoryOnly = isDirectoryOnly;
            public readonly Regex Regex = regex;
        }

        private readonly List<CompiledRule> _compiled;

        /// <summary>
        /// Creates a new rule set from parsed .gitignore rules.
        /// </summary>
        /// <param name="rules">Parsed rules in the order they were declared.</param>
        public GitIgnoreRuleSet(IReadOnlyList<GitIgnoreParser.GitIgnoreRule> rules)
        {
            if (rules is null || rules.Count == 0)
            {
                _compiled = [];
                return;
            }

            var list = new List<CompiledRule>(rules.Count);
            foreach (var r in rules)
            {
                var regex = BuildRegex(r.NormalizedPattern, r.IsAnchored, r.IsDirectoryOnly);
                list.Add(new CompiledRule(r.IsNegation, r.IsDirectoryOnly, regex));
            }
            _compiled = list;
        }

        /// <summary>
        /// Returns true if the given path is ignored by this rule set.
        /// </summary>
        /// <remarks>
        /// .gitignore semantics are order-sensitive: the **last matching rule wins**.
        /// To implement this efficiently and correctly, we iterate the compiled rules
        /// in reverse order and return on the first match we encounter:
        ///   - If that rule is a negation ('!'), the path is **not** ignored.
        ///   - Otherwise, the path **is** ignored.
        /// Directory-only rules (patterns ending with '/') apply only to directories.
        /// </remarks>
        /// <param name="relativePath">Path relative to the scan root, using forward slashes ('/').</param>
        /// <param name="isDirectory">True if the path refers to a directory.</param>
        /// <returns>True if the path should be ignored; otherwise false.</returns>
        public bool IsIgnored(string relativePath, bool isDirectory)
        {
            if (string.IsNullOrEmpty(relativePath) || _compiled.Count == 0)
                return false;

            var path = Normalize(relativePath);

            for (int i = _compiled.Count - 1; i >= 0; i--)
            {
                var rule = _compiled[i];
                if (rule.IsDirectoryOnly && !isDirectory) continue;
                if (rule.Regex.IsMatch(path))
                    return !rule.IsNegation; // last match found, stop
            }
            return false; // no rule matched
        }

        private static string Normalize(string p)
        {
            p = p.Replace('\\', '/');
            if (p.StartsWith("./", StringComparison.Ordinal))
                p = p[2..];
            if (p.Length > 1 && p.EndsWith('/'))
                p = p.TrimEnd('/');
            return p;
        }

        /// <summary>
        /// Builds a compiled regex from a gitignore-like pattern.
        /// Handles **, *, ?, and character classes [..] including negation [!..].
        /// </summary>
        private static Regex BuildRegex(string pattern, bool anchored, bool dirOnly)
        {
            // We build the regex manually (no Regex.Escape over the whole string)
            // to preserve character classes like [Dd]ebug, [a-z], [!abc].
            var sb = new StringBuilder();

            // Anchor handling: if not anchored, allow match to start at any segment boundary.
            // We use a non-capturing prefix "(?:^|.*/)".
            if (anchored)
                sb.Append('^');
            else
                sb.Append("(?:^|.*/)");

            // Convert the glob pattern into regex
            AppendPatternAsRegex(sb, pattern);

            // Directory-only rules: match the directory itself or anything under it.
            if (dirOnly)
                sb.Append("(?:/.*)?$");
            else
                sb.Append('$');

            return new Regex(sb.ToString(), RegexOptions.Compiled | RegexOptions.CultureInvariant);
        }

        /// <summary>
        /// Appends the regex equivalent of the given glob pattern to <paramref name="sb"/>.
        /// Supports:
        ///  - '**'  => '.*'      (can cross '/')
        ///  - '*'   => '[^/]*'   (cannot cross '/')
        ///  - '?'   => '[^/]'    (single char, not '/')
        ///  - '[..]' character classes with optional leading '!' for negation
        /// All other regex metacharacters are escaped.
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
                            // '**' => '.*' ; '*' => '[^/]*'
                            if (i + 1 < pat.Length && pat[i + 1] == '*')
                            {
                                sb.Append(".*");
                                i++; // consume the second '*'
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
                            // Handle negation inside class: [!abc] -> [^abc]
                            if (i + 1 < pat.Length && (pat[i + 1] == '!' || pat[i + 1] == '^'))
                            {
                                sb.Append('^');
                                i++; // consume the '!' or '^'
                            }
                            break;

                        // Escape regex metacharacters outside classes
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

                        // Slash remains slash (path separator)
                        case '/':
                            sb.Append('/');
                            break;

                        // Everything else is literal
                        default:
                            sb.Append(c);
                            break;
                    }
                }
                else
                {
                    // Inside a character class until we hit an unescaped ']'
                    if (c == ']')
                    {
                        inClass = false;
                        sb.Append(']');
                    }
                    else
                    {
                        // Minimal escaping inside class: only escape '\' and (optionally) '-'
                        // We keep '-' as-is to allow ranges like a-z.
                        if (c == '\\')
                            sb.Append(@"\\");
                        else
                            sb.Append(c);
                    }
                }
            }

            // If the pattern had an unterminated class, treat '[' literally by escaping it.
            if (inClass)
            {
                // Replace the trailing '[' with a literal '\['
                // (Simple approach: append '\[' at the end to avoid invalid regex)
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