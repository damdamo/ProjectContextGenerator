using ProjectContextGenerator.Domain.Abstractions;

namespace ProjectContextGenerator.Tests.Fakes
{
    public sealed class FakeFileSystem : IFileSystem
    {
        private readonly HashSet<string> _dirs;
        private readonly HashSet<string> _files;
        private readonly Dictionary<string, string> _fileContents;

        public FakeFileSystem(IEnumerable<string> directories, IEnumerable<string> files)
        {
            // Normalise to absolute-like roots for simplicity in tests
            _dirs = directories
                .Select(NormDir)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            _files = files
                .Select(NormFile)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            _fileContents = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);

            // Ensure parents exist
            foreach (var d in _dirs.ToList())
            {
                var cur = d.TrimEnd('/');
                while (cur.Contains('/'))
                {
                    cur = cur[..cur.LastIndexOf('/')];
                    _dirs.Add(cur + "/");
                }
            }
        }

        public IEnumerable<string> EnumerateDirectories(string path)
        {
            var prefix = NormDir(path);
            var depth = prefix.Count(c => c == '/');
            // Return only immediate children
            return _dirs.Where(d =>
            {
                if (!d.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;
                if (d.Equals(prefix, StringComparison.OrdinalIgnoreCase)) return false;
                return d.Count(c => c == '/') == depth + 1;
            }).Select(DenormDir);
        }

        public IEnumerable<string> EnumerateFiles(string path)
        {
            var prefix = NormDir(path);
            return _files.Where(f =>
            {
                var folder = f[..(f.LastIndexOf('/') + 1)];
                return folder.Equals(prefix, StringComparison.OrdinalIgnoreCase);
            }).Select(DenormFile);
        }

        public string GetFileName(string path)
        {
            var p = path.Replace('\\', '/').TrimEnd('/');
            var idx = p.LastIndexOf('/');
            return idx >= 0 ? p[(idx + 1)..] : p;
        }
        public bool FileExists(string path)
            => _files.Contains(NormFile(path));

        public string ReadAllText(string path)
        {
            var p = NormFile(path);
            if (!_files.Contains(p))
                throw new FileNotFoundException($"File not found: {path}");

            return _fileContents.TryGetValue(p, out var content) ? content : string.Empty;
        }

        // Helpers
        private static string NormDir(string p)
        {
            var s = p.Replace('\\', '/').TrimEnd('/');
            return s + "/";
        }
        private static string NormFile(string p) => p.Replace('\\', '/');
        private static string DenormDir(string p) => p.TrimEnd('/'); // TreeBuilder will pass these back to GetFileName etc.
        private static string DenormFile(string p) => p;
    }
}