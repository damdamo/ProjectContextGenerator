using System;
using System.Collections.Generic;
using System.IO;
using ProjectContextGenerator.Domain.Config;
using ProjectContextGenerator.Domain.Options;
using Xunit;

namespace ProjectContextGenerator.Tests.ConfigTests
{
    public sealed class ContextConfigMapperTests
    {
        private static string MakeTempDir()
        {
            var dir = Path.Combine(Path.GetTempPath(), "treecfg_tests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        [Fact]
        public void Map_NormalizesPatterns_DirectoryShorthand_ExtensionShorthand_PlainNames()
        {
            var cfgDir = MakeTempDir();
            try
            {
                var dto = new ContextConfigDto
                {
                    Version = 1,
                    Include = ["*.cs", "README", "**/*.csproj"],
                    Exclude = ["bin/", "obj/", ".git/"]
                };

                var (options, _, _, root, diags) = ContextConfigMapper.Map(dto, profileName: null, configDirectory: cfgDir, rootOverride: null);

                Assert.Equal(1, dto.Version);
                Assert.Empty(diags);
                Assert.Equal(Path.GetFullPath(".", cfgDir), root);

                // Include
                Assert.NotNull(options.IncludeGlobs);
                Assert.Contains("**/*.cs", options.IncludeGlobs!);     // from "*.cs"
                Assert.Contains("**/README", options.IncludeGlobs!);   // from "README"
                Assert.Contains("**/*.csproj", options.IncludeGlobs!); // already globbed

                // Exclude 
                Assert.NotNull(options.ExcludeGlobs);
                Assert.Contains("**/bin/**", options.ExcludeGlobs!);
                Assert.Contains("**/obj/**", options.ExcludeGlobs!);
                Assert.Contains("**/.git/**", options.ExcludeGlobs!);
            }
            finally { Directory.Delete(cfgDir, true); }
        }

        [Fact]
        public void Map_RootResolution_Priority_CLI_Overrides_Config_Overrides_Default()
        {
            // Case 1: no CLI override → config root resolved against config directory
            var cfgDir1 = MakeTempDir();
            try
            {
                var dto = new ContextConfigDto { Version = 1, Root = "relative/from/config" };
                var (_, _, _, root, diags) = ContextConfigMapper.Map(dto, null, cfgDir1, null);
                var expected = Path.GetFullPath("relative/from/config", cfgDir1);
                Assert.Equal(expected, root);
                Assert.Empty(diags);
            }
            finally { Directory.Delete(cfgDir1, true); }

            // Case 2: CLI override (relative) → resolved against CWD
            var cfgDir2 = MakeTempDir();
            try
            {
                var dto = new ContextConfigDto { Version = 1, Root = "relative/from/config" };
                var (_, _, _, root, diags) = ContextConfigMapper.Map(dto, null, cfgDir2, rootOverride: "relative/from/cli");
                var expected = Path.GetFullPath("relative/from/cli", Environment.CurrentDirectory);
                Assert.Equal(expected, root);
                Assert.Empty(diags);
            }
            finally { Directory.Delete(cfgDir2, true); }

            // Case 3: both absolute roots pass through
            var cfgDir3 = MakeTempDir();
            try
            {
                var absConfig = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "configRoot_" + Guid.NewGuid().ToString("N")));
                var absCli = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "cliRoot_" + Guid.NewGuid().ToString("N")));
                var dto2 = new ContextConfigDto { Version = 1, Root = absConfig };

                var (_, _, _, root, diags) = ContextConfigMapper.Map(dto2, null, cfgDir3, absCli);
                Assert.Equal(Path.GetFullPath(absCli), root);
                Assert.Empty(diags);
            }
            finally { Directory.Delete(cfgDir3, true); }
        }

        [Fact]
        public void Map_ProfileNotFound_ProducesDiagnostic_And_FallsBackToRootConfig()
        {
            var cfgDir = MakeTempDir();
            try
            {
                var dto = new ContextConfigDto
                {
                    Version = 1,
                    MaxDepth = 2,
                    Profiles = new Dictionary<string, ContextConfigDto>
                    {
                        ["fast"] = new() { MaxDepth = 1 }
                    }
                };

                var (options, _, _, _, diags) = ContextConfigMapper.Map(dto, profileName: "does-not-exist", configDirectory: cfgDir, rootOverride: null);

                Assert.Contains(diags, d => d.Contains("Profile 'does-not-exist' not found", StringComparison.OrdinalIgnoreCase));
                Assert.Equal(2, options.MaxDepth); // fallback to root config
            }
            finally { Directory.Delete(cfgDir, true); }
        }

        [Fact]
        public void Map_UnsupportedVersion_ProducesDiagnostic_ButStillBuildsOptions()
        {
            var cfgDir = MakeTempDir();
            try
            {
                var dto = new ContextConfigDto { Version = 42, MaxDepth = 3 };
                var (options, _, _, _, diags) = ContextConfigMapper.Map(dto, null, cfgDir, null);

                Assert.Contains(diags, d => d.Contains("Unsupported config version '42'", StringComparison.OrdinalIgnoreCase));
                Assert.Equal(3, options.MaxDepth);
            }
            finally { Directory.Delete(cfgDir, true); }
        }

        [Theory]
        [InlineData(-2, -1)] // invalid -> coerced to -1 with diagnostic
        [InlineData(-1, -1)] // unlimited accepted
        [InlineData(0, 0)]
        [InlineData(5, 5)]
        public void Map_MaxDepth_Validation(int input, int expected)
        {
            var cfgDir = MakeTempDir();
            try
            {
                var dto = new ContextConfigDto { Version = 1, MaxDepth = input };
                var (options, _, _, _, diags) = ContextConfigMapper.Map(dto, null, cfgDir, null);

                Assert.Equal(expected, options.MaxDepth);
                if (input < -1)
                    Assert.Contains(diags, d => d.Contains("Invalid maxDepth", StringComparison.OrdinalIgnoreCase));
                else
                    Assert.DoesNotContain(diags, d => d.Contains("Invalid maxDepth", StringComparison.OrdinalIgnoreCase));
            }
            finally { Directory.Delete(cfgDir, true); }
        }

        [Fact]
        public void Map_MaxItemsPerDirectory_Validation()
        {
            var cfgDir = MakeTempDir();
            try
            {
                // invalid negative -> ignored with diagnostic
                var dto = new ContextConfigDto { Version = 1, MaxItemsPerDirectory = -5 };
                var (o1, _, _, _, d1) = ContextConfigMapper.Map(dto, null, cfgDir, null);
                Assert.Null(o1.MaxItemsPerDirectory);
                Assert.Contains(d1, d => d.Contains("Invalid maxItemsPerDirectory", StringComparison.OrdinalIgnoreCase));

                // valid
                var dto2 = new ContextConfigDto { Version = 1, MaxItemsPerDirectory = 10 };
                var (o2, _, _, _, d2) = ContextConfigMapper.Map(dto2, null, cfgDir, null);
                Assert.Equal(10, o2.MaxItemsPerDirectory);
                Assert.DoesNotContain(d2, d => d.Contains("Invalid maxItemsPerDirectory", StringComparison.OrdinalIgnoreCase));
            }
            finally { Directory.Delete(cfgDir, true); }
        }

        [Fact]
        public void Map_Defaults_AreApplied_WhenFieldsAreNull()
        {
            var cfgDir = MakeTempDir();
            try
            {
                var dto = new ContextConfigDto(); // everything null
                var (o, _, _, root, diags) = ContextConfigMapper.Map(dto, null, cfgDir, null);

                Assert.Equal(4, o.MaxDepth); // default
                Assert.True(o.SortDirectoriesFirst);
                Assert.True(o.CollapseSingleChildDirectories);
                Assert.Equal(GitIgnoreMode.RootOnly, o.GitIgnore);
                Assert.Equal(".gitignore", o.GitIgnoreFileName);
                Assert.False(o.DirectoriesOnly);
                Assert.Null(o.IncludeGlobs);
                Assert.Null(o.ExcludeGlobs);
                Assert.Empty(diags);
                Assert.Equal(Path.GetFullPath(".", cfgDir), root);
            }
            finally { Directory.Delete(cfgDir, true); }
        }

        [Fact]
        public void Map_Profile_Merges_ByOverride_NotConcatenation()
        {
            var cfgDir = MakeTempDir();
            try
            {
                // The profile replaces lists when set (no implicit concatenation).
                var dto = new ContextConfigDto
                {
                    Version = 1,
                    Include = ["**/*.md"],
                    Exclude = ["**/bin/**"],
                    Profiles = new Dictionary<string, ContextConfigDto>
                    {
                        ["csharp"] = new()
                        {
                            Include = ["*.cs", "*.csproj"], // -> **/*.cs, **/*.csproj
                            Exclude = ["obj/"]              // -> **/obj/**
                        }
                    }
                };

                var (o, _, _, _, diags) = ContextConfigMapper.Map(dto, "csharp", cfgDir, null);

                Assert.NotNull(o.IncludeGlobs);
                Assert.DoesNotContain(o.IncludeGlobs!, g => g.EndsWith(".md", StringComparison.OrdinalIgnoreCase));
                Assert.Contains("**/*.cs", o.IncludeGlobs!);
                Assert.Contains("**/*.csproj", o.IncludeGlobs!);

                Assert.NotNull(o.ExcludeGlobs);
                Assert.DoesNotContain(o.ExcludeGlobs!, g => g.Contains("/bin/"));
                Assert.Contains("**/obj/**", o.ExcludeGlobs!);
                Assert.Empty(diags);
            }
            finally { Directory.Delete(cfgDir, true); }
        }
    }
}