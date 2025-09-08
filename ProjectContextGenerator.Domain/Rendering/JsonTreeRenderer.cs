using ProjectContextGenerator.Domain.Abstractions;
using ProjectContextGenerator.Domain.Models;

namespace ProjectContextGenerator.Domain.Rendering
{
    public sealed class JsonTreeRenderer : ITreeRenderer
    {
        public string Render(DirectoryNode root)
            => System.Text.Json.JsonSerializer.Serialize(root,
               options: new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
    }

}
