using ProjectContextGenerator.Domain.Models;
using ProjectContextGenerator.Domain.Options;

namespace ProjectContextGenerator.Domain.Abstractions
{
    public interface ITreeBuilder
    {
        DirectoryNode Build(string rootPath, TreeScanOptions options);
    }

}
