using ProjectContextGenerator.Domain.Models;

namespace ProjectContextGenerator.Domain.Abstractions
{
    /// <summary>
    /// Defines a contract for rendering a directory tree into a string
    /// representation (e.g., Markdown, plain text).
    /// </summary>
    public interface ITreeRenderer
    {
        /// <summary>
        /// Renders the given <see cref="DirectoryNode"/> and its children
        /// into a textual representation.
        /// </summary>
        /// <param name="root">The root directory node of the tree to render.</param>
        /// <returns>A string containing the rendered tree.</returns>
        string Render(DirectoryNode root);
    }
}
