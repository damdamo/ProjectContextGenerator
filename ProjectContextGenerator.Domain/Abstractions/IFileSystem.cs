namespace ProjectContextGenerator.Domain.Abstractions
{
    /// <summary>
    /// Abstraction of a file system, used to enumerate directories and files
    /// and to extract file names in a platform-agnostic way.
    /// </summary>
    public interface IFileSystem
    {
        /// <summary>
        /// Enumerates all subdirectories within the specified path.
        /// </summary>
        /// <param name="path">The absolute or relative directory path to search.</param>
        /// <returns>A sequence of full directory paths.</returns>
        IEnumerable<string> EnumerateDirectories(string path);

        /// <summary>
        /// Enumerates all files within the specified path.
        /// </summary>
        /// <param name="path">The absolute or relative directory path to search.</param>
        /// <returns>A sequence of full file paths.</returns>
        IEnumerable<string> EnumerateFiles(string path);

        /// <summary>
        /// Extracts the file or directory name (the last path segment) from a full path.
        /// </summary>
        /// <param name="path">The full file or directory path.</param>
        /// <returns>The final segment of the path (file or folder name).</returns>
        string GetFileName(string path);
    }
}
