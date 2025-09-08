using ProjectContextGenerator.Domain.Models;

namespace ProjectContextGenerator.Domain.Abstractions
{
    public interface ITreeRenderer
    {
        public string Render(DirectoryNode root);
    }
}
