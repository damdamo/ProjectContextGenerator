using System.Text.Json;
using ProjectContextGenerator.Domain.Config;

namespace ProjectContextGenerator.Infrastructure.Config
{
    public static class JsonFileConfigLoader
    {
        private static readonly JsonSerializerOptions s_options = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip, // allows JSON with comments if needed
            AllowTrailingCommas = true
        };

        /// <summary>
        /// Loads a TreeConfigDto from a JSON file and returns the DTO plus the directory containing the file.
        /// Throws FileNotFoundException or JsonException on errors.
        /// </summary>
        public static (TreeConfigDto Config, string ConfigDirectory) Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Config path is null or empty.", nameof(path));

            var fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath))
                throw new FileNotFoundException($"Config file not found: {fullPath}", fullPath);

            var json = File.ReadAllText(fullPath);
            var dto = JsonSerializer.Deserialize<TreeConfigDto>(json, s_options)
                      ?? throw new JsonException("Config file deserialized to null DTO.");

            var directory = Path.GetDirectoryName(fullPath)
                            ?? throw new InvalidOperationException("Unable to determine config directory.");

            return (dto, directory);
        }
    }
}
