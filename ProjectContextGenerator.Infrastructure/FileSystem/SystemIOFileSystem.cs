using ProjectContextGenerator.Domain.Abstractions;

namespace ProjectContextGenerator.Infrastructure.FileSystem
{
    public sealed class SystemIOFileSystem : IFileSystem
    {
        public IEnumerable<string> EnumerateDirectories(string path) =>
            Directory.EnumerateDirectories(path);

        public IEnumerable<string> EnumerateFiles(string path) =>
            Directory.EnumerateFiles(path);

        public string GetFileName(string path) =>
            Path.GetFileName(path);

        public bool FileExists(string path) =>
            File.Exists(path);

        public string ReadAllText(string path) =>
            File.ReadAllText(path); // UTF-8 by default on .NET
    }
}