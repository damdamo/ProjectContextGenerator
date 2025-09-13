using ProjectContextGenerator.Domain.Models;
using ProjectContextGenerator.Domain.Options;

namespace ProjectContextGenerator.Domain.Abstractions
{
    /// <summary>
    /// Defines a contract for building a hierarchical tree representation of
    /// directories and files, starting from a root path.
    /// </summary>
    public interface ITreeBuilder
    {
        /// <summary>
        /// Builds a tree of <see cref="DirectoryNode"/> objects, beginning at the
        /// specified root path, applying the provided scan options.
        /// </summary>
        /// <param name="rootPath">The root path where the tree scan begins.</param>
        /// <param name="options">Options controlling tree construction, such as max depth or sorting.</param>
        /// <returns>The root directory node representing the scanned tree.</returns>
        DirectoryNode Build(string rootPath, TreeScanOptions options);
    }
}
