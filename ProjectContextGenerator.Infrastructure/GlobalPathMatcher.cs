using Microsoft.Extensions.FileSystemGlobbing;
using ProjectContextGenerator.Domain.Abstractions;
using System.Collections.Frozen;
using System.Text.RegularExpressions;

namespace ProjectContextGenerator.Infrastructure
{
    public sealed class GlobPathMatcher : IPathMatcher
    {
        private readonly Matcher _include = new(StringComparison.OrdinalIgnoreCase);
        private readonly Matcher _exclude = new(StringComparison.OrdinalIgnoreCase);
        private readonly bool _hasExcludes;

        // Fast path for directories (handles empty dirs too)
        private readonly HashSet<string> _excludedFolderNames;
        private readonly HashSet<string> _excludedFolderPrefixes; // e.g. "build/output"

        public GlobPathMatcher(IEnumerable<string>? includeGlobs, IEnumerable<string>? excludeGlobs)
        {
            var inc = (includeGlobs is { } i && i.Any()) ? [.. Normalise(i)] : new[] { "**/*" };
            var exc = (excludeGlobs is { } e && e.Any()) ? [.. Normalise(e)] : Array.Empty<string>();

            _include.AddIncludePatterns(inc);
            if (exc.Length > 0)
            {
                // Add a default include ("**/*") so that this matcher accepts everything by default.
                // Then apply the actual exclude patterns. Without an include, the matcher would never
                // return true (because it requires at least one include match). This way, we get a
                // proper "include all, then exclude some" behavior.
                //
                // Example:
                //   Excludes = ["**/*.cs"]
                //   → "Foo/File.cs"   => _exclude.Match(...) == false (excluded)
                //   → "Foo/Lib.csproj"=> _exclude.Match(...) == true  (kept)
                _exclude.AddInclude("**/*");
                _exclude.AddExcludePatterns(exc);
            }

            _hasExcludes = exc.Length > 0;

            (_excludedFolderNames, _excludedFolderPrefixes) = ExtractFolderExclusions(exc);
        }

        public bool IsMatch(string relativePath, bool isDirectory)
        {
            // normalise to forward slashes, no leading "./"
            var p = relativePath.Replace('\\', '/');
            if (p.StartsWith("./"))
                p = p[2..];

            if (isDirectory)
            {
                // 1) Fast folder checks (works for empty folders too)
                if (IsFolderExcludedByNameOrPrefix(p)) return false;

                // 2) Fallback to globbing by probing a fictitious file within the folder
                var dir = p.EndsWith('/') ? p : p + '/';
                var probe = dir + "__any__";

                var inc = _include.Match(probe).HasMatches || _include.Match(dir).HasMatches;
                if (!inc) return false;

                if (_hasExcludes)
                    return _exclude.Match(probe).HasMatches || _exclude.Match(dir).HasMatches;
                
                return true;
            }
            else
            {
                // Files are handled directly by the globber
                var inc = _include.Match(p).HasMatches;
                if (!inc) return false;

                if (!_hasExcludes) return true;
                return _exclude.Match(p).HasMatches;
            }
        }

        private static IEnumerable<string> Normalise(IEnumerable<string> patterns)
        {
            foreach (var raw in patterns)
            {
                var s = raw.Replace('\\', '/').Trim();
                if (string.IsNullOrWhiteSpace(s)) continue;

                // Users often write "obj/" → treat as "obj/**"
                if (s.EndsWith('/')) s += "**";
                yield return s;
            }
        }

        private static (HashSet<string> names, HashSet<string> prefixes) ExtractFolderExclusions(IEnumerable<string> excludeGlobs)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var prefixes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var g in excludeGlobs)
            {
                var s = g.Replace('\\', '/');

                // Common folder patterns we want to catch quickly:
                //   "obj/**", "**/obj/**", "build/output/**", "/bin/**"
                if (s.EndsWith("/**", StringComparison.Ordinal))
                {
                    var folderPath = s[..^3].TrimStart('/'); // remove /** and leading /
                    if (folderPath.Length == 0) continue;

                    prefixes.Add(folderPath);                       // e.g. "build/output"
                    var lastSegment = folderPath.Split('/').Last(); // e.g. "obj"
                    if (lastSegment.Length > 0) names.Add(lastSegment);
                    continue;
                }

                // If pattern is exactly a single folder segment (rare), capture it
                if (!s.Contains('*') && !s.Contains('?') && !s.EndsWith('/'))
                {
                    // treat as a prefix too (user might have written "obj" instead of "obj/**")
                    prefixes.Add(s.TrimStart('/'));
                    var last = s.Split('/').Last();
                    if (last.Length > 0) names.Add(last);
                }
            }

            return (names.ToHashSet(StringComparer.OrdinalIgnoreCase),
                    prefixes.ToHashSet(StringComparer.OrdinalIgnoreCase));
        }

        private bool IsFolderExcludedByNameOrPrefix(string folderRelativePath)
        {
            var trimmed = folderRelativePath.TrimEnd('/');

            // prefix match: "build/output" should exclude "build/output", "build/output/sub/…"
            foreach (var prefix in _excludedFolderPrefixes)
            {
                if (trimmed.Equals(prefix, StringComparison.OrdinalIgnoreCase) ||
                    trimmed.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            // name match: if any segment equals a banned name (e.g. "obj", "bin")
            foreach (var segment in trimmed.Split('/'))
            {
                if (_excludedFolderNames.Contains(segment)) return true;
            }

            return false;
        }
    }
}