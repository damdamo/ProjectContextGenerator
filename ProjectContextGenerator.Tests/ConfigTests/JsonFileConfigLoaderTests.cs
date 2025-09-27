using ProjectContextGenerator.Domain.Config;
using ProjectContextGenerator.Infrastructure.Config;
using Xunit;

namespace ProjectContextGenerator.Tests.ConfigTests
{
    public sealed class JsonFileConfigLoaderTests
    {
        [Fact]
        public void Load_Throws_When_File_NotFound()
        {
            var ex = Assert.Throws<FileNotFoundException>(() => JsonFileConfigLoader.Load("/path/that/does/not/exist.json"));
            Assert.Contains("Config file not found", ex.Message);
        }

        [Fact]
        public void Load_Throws_When_Invalid_Json()
        {
            var tmp = Path.Combine(Path.GetTempPath(), $"bad_config_{Guid.NewGuid():N}.json");
            File.WriteAllText(tmp, "{ invalid json,,, ");

            try
            {
                Assert.Throws<System.Text.Json.JsonException>(() => JsonFileConfigLoader.Load(tmp));
            }
            finally
            {
                File.Delete(tmp);
            }
        }

        [Fact]
        public void Load_Parses_Valid_File_And_Returns_Dto_And_Directory()
        {
            var tmpDir = Path.Combine(Path.GetTempPath(), $"cfg_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tmpDir);
            var tmpFile = Path.Combine(tmpDir, ".treegen.json");

            var json = """
            {
              "version": 1,
              "root": ".",
              "maxDepth": 3,
              "exclude": ["bin/", "obj/"],
              "gitIgnore": "Nested",
              "profiles": {
                "csharp": {
                  "include": ["*.cs", "*.csproj"],
                  "exclude": ["obj/"]
                }
              }
            }
            """;

            try
            {
                File.WriteAllText(tmpFile, json);
                var (dto, dir) = JsonFileConfigLoader.Load(tmpFile);

                Assert.NotNull(dto);
                Assert.Equal(1, dto.Version);
                Assert.Equal(".", dto.Root);
                Assert.Equal(3, dto.MaxDepth);
                Assert.NotNull(dto.Exclude);
                Assert.Equal("Nested", dto.GitIgnore);
                Assert.NotNull(dto.Profiles);
                Assert.True(dto.Profiles!.ContainsKey("csharp"));
                Assert.Equal(tmpDir, dir);
            }
            finally
            {
                File.Delete(tmpFile);
                Directory.Delete(tmpDir, recursive: true);
            }
        }
    }
}