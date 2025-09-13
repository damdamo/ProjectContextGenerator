using System.Text;

namespace ProjectContextGenerator.Infrastructure.GitIgnore
{
    /// <summary>
    /// Internal helper that parses .gitignore text into a list of normalized rules.
    /// Parsing responsibility only — no evaluation or file system I/O.
    /// </summary>
    public static class GitIgnoreParser
    {
        /// <summary>
        /// Represents a single parsed .gitignore rule with normalized metadata.
        /// </summary>
        /// <param name="OriginalPattern">The original pattern as it appears in the file.</param>
        /// <param name="NormalizedPattern">A normalized pattern using forward slashes ('/') and no trailing whitespace.</param>
        /// <param name="IsNegation">True if the rule starts with '!' (un-ignore).</param>
        /// <param name="IsDirectoryOnly">True if the rule targets directories only (pattern ends with '/').</param>
        /// <param name="IsAnchored">True if the rule is rooted at the repository root (pattern starts with '/').</param>
        public readonly record struct GitIgnoreRule(
            string OriginalPattern,
            string NormalizedPattern,
            bool IsNegation,
            bool IsDirectoryOnly,
            bool IsAnchored
        );

        /// <summary>
        /// Parses .gitignore content into a sequence of rules in file order (last rule wins).
        /// Handles comments (#), blank lines, and escaped leading '!' or '#'.
        /// </summary>
        /// <param name="content">Full textual content of a .gitignore file.</param>
        /// <returns>Parsed rules in declaration order.</returns>
        internal static IReadOnlyList<GitIgnoreRule> Parse(string content)
        {
            var rules = new List<GitIgnoreRule>();
            if (string.IsNullOrEmpty(content))
                return rules;

            foreach (var rawLine in SplitLines(content))
            {
                var line = rawLine.Replace('\\', '/'); // normalize path separators
                if (line.Length == 0) continue;

                // Strip trailing \r (in case of \r\n already split by \n)
                if (line[^1] == '\r') line = line[..^1];

                // Skip comments and blank lines
                if (line.Length == 0) continue;

                // Handle escaped leading '#' or '!' with backslash: "\#" or "\!" are literals.
                var escapedLiteral = false;
                if (line.StartsWith(@"\#") || line.StartsWith(@"\!"))
                {
                    line = line[1..]; // drop the backslash, keep the char
                    escapedLiteral = true;
                }

                // If not escaped and starts with '#', it is a comment line → skip.
                if (!escapedLiteral && line.StartsWith('#'))
                    continue;

                // Trim surrounding whitespace (git is whitespace-sensitive inside the token,
                // but leading/trailing spaces are usually not significant for our purposes).
                line = line.Trim();

                if (line.Length == 0) continue;

                // Negation
                var isNegation = false;
                if (!escapedLiteral && line[0] == '!')
                {
                    isNegation = true;
                    line = line[1..];
                    if (line.Length == 0)
                        continue; // a bare "!" line is invalid; ignore it
                }

                // Directory-only (pattern ending with '/')
                var isDirOnly = line.EndsWith('/');
                if (isDirOnly)
                    line = line.TrimEnd('/');

                // Anchored (pattern starting with '/')
                var isAnchored = line.StartsWith('/');
                if (isAnchored)
                    line = line.TrimStart('/');

                // Normalize (collapse duplicate slashes, strip "./")
                line = NormalizePattern(line);
                if (line.Length == 0)
                    continue;

                rules.Add(new GitIgnoreRule(
                    OriginalPattern: rawLine,
                    NormalizedPattern: line,
                    IsNegation: isNegation,
                    IsDirectoryOnly: isDirOnly,
                    IsAnchored: isAnchored
                ));
            }

            return rules;
        }

        private static IEnumerable<string> SplitLines(string text)
        {
            int i = 0, start = 0;
            while (i < text.Length)
            {
                var c = text[i++];
                if (c == '\n')
                {
                    yield return text[start..(i - 1)];
                    start = i;
                }
            }
            // tail
            if (start <= text.Length)
                yield return text[start..];
        }

        private static string NormalizePattern(string p)
        {
            if (p.StartsWith("./", StringComparison.Ordinal))
                p = p[2..];

            // Collapse multiple slashes
            var span = p.AsSpan();
            var sb = new StringBuilder(span.Length);
            char prev = '\0';
            foreach (var ch in span)
            {
                if (ch == '/' && prev == '/')
                    continue;
                sb.Append(ch);
                prev = ch;
            }
            return sb.ToString();
        }
    }
}
