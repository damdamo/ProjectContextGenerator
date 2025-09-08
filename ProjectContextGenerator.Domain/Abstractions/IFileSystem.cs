namespace ProjectContextGenerator.Domain.Abstractions
{
    public interface IFileSystem
    {
        IEnumerable<string> EnumerateDirectories(string path);
        IEnumerable<string> EnumerateFiles(string path);
        string GetFileName(string path);
    }
}
