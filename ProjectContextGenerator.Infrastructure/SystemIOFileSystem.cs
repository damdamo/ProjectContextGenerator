using ProjectContextGenerator.Domain.Abstractions;

namespace ProjectContextGenerator.Infrastructure
{
    public sealed class SystemIOFileSystem : IFileSystem
    {
        public IEnumerable<string> EnumerateDirectories(string path) =>
            Directory.EnumerateDirectories(path);

        public IEnumerable<string> EnumerateFiles(string path) =>
            Directory.EnumerateFiles(path);

        public string GetFileName(string path) =>
            Path.GetFileName(path);
    }
}