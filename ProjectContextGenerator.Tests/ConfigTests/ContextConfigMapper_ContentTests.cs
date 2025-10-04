using ProjectContextGenerator.Domain.Config;

namespace ProjectContextGenerator.Tests.ConfigTests
{
    public class ContextConfigMapper_ContentTests
    {
        private static string MakeTempDir()
        {
            var dir = Path.Combine(Path.GetTempPath(), "contentcfg_tests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        [Fact]
        public void Defaults_WhenContentBlockMissing_ShouldUseSafeDefaults()
        {
            var cfgDir = MakeTempDir();
            try
            {
                var dto = new ContextConfigDto { Version = 1 };

                // Map returns: (Options, History, Content, Root, Diagnostics)
                var (_, _, content, _, diags) = ContextConfigMapper.Map(dto, null, cfgDir, null);

                Assert.False(content.Enabled);
                Assert.Equal(1, content.IndentDepth);
                Assert.Equal(4, content.TabWidth);
                Assert.True(content.DetectTabWidth);
                Assert.Equal(300, content.MaxLinesPerFile);
                Assert.False(content.ShowLineNumbers);
                Assert.Equal(1, content.ContextPadding);
                Assert.Null(content.MaxFiles); // default is null (no global cap)
                Assert.Empty(diags);
            }
            finally { Directory.Delete(cfgDir, true); }
        }

        [Fact]
        public void ContentOptions_ShouldMapAllProperties()
        {
            var cfgDir = MakeTempDir();
            try
            {
                var dto = new ContextConfigDto
                {
                    Content = new ContentDto
                    {
                        Enabled = true,
                        IndentDepth = 2,
                        TabWidth = 2,
                        DetectTabWidth = false,
                        MaxLinesPerFile = 42,
                        ShowLineNumbers = true,
                        ContextPadding = 5,
                        MaxFiles = 10
                    }
                };

                var (_, _, content, _, diags) = ContextConfigMapper.Map(dto, null, cfgDir, null);

                Assert.True(content.Enabled);
                Assert.Equal(2, content.IndentDepth);
                Assert.Equal(2, content.TabWidth);
                Assert.False(content.DetectTabWidth);
                Assert.Equal(42, content.MaxLinesPerFile);
                Assert.True(content.ShowLineNumbers);
                Assert.Equal(5, content.ContextPadding);
                Assert.Equal(10, content.MaxFiles);
                Assert.Empty(diags);
            }
            finally { Directory.Delete(cfgDir, true); }
        }

        [Fact]
        public void ContentOptions_ProfileOverridesRoot()
        {
            var cfgDir = MakeTempDir();
            try
            {
                var dto = new ContextConfigDto
                {
                    Content = new ContentDto { Enabled = false, IndentDepth = 1 },
                    Profiles = new Dictionary<string, ContextConfigDto>
                    {
                        ["full"] = new ContextConfigDto
                        {
                            Content = new ContentDto { Enabled = true, IndentDepth = -1 }
                        }
                    }
                };

                var (_, _, content, _, diags) = ContextConfigMapper.Map(dto, "full", cfgDir, null);

                Assert.True(content.Enabled);
                Assert.Equal(-1, content.IndentDepth);
                Assert.Empty(diags);
            }
            finally { Directory.Delete(cfgDir, true); }
        }

        [Fact]
        public void Map_Content_TabWidth_Suspicious_FallsBackToDefault_WithDiagnostic()
        {
            var cfgDir = MakeTempDir();
            try
            {
                var dto = new ContextConfigDto
                {
                    Version = 1,
                    Content = new ContentDto
                    {
                        Enabled = true,
                        TabWidth = 3  // suspicious -> fallback to 4 + diag
                    }
                };

                var (_, _, content, _, diags) = ContextConfigMapper.Map(dto, null, cfgDir, null);
                Assert.Equal(4, content.TabWidth);
                Assert.Contains(diags, d => d.Contains("Suspicious content.tabWidth", StringComparison.OrdinalIgnoreCase));
            }
            finally { Directory.Delete(cfgDir, true); }
        }

        [Fact]
        public void Map_Content_MaxLinesPerFile_Zero_FallsBack_WithDiagnostic()
        {
            var cfgDir = MakeTempDir();
            try
            {
                var dto = new ContextConfigDto
                {
                    Version = 1,
                    Content = new ContentDto
                    {
                        Enabled = true,
                        MaxLinesPerFile = 0 // invalid -> fallback 300 + diag
                    }
                };

                var (_, _, content, _, diags) = ContextConfigMapper.Map(dto, null, cfgDir, null);
                Assert.Equal(300, content.MaxLinesPerFile);
                Assert.Contains(diags, d => d.Contains("Invalid content.maxLinesPerFile", StringComparison.OrdinalIgnoreCase));
            }
            finally { Directory.Delete(cfgDir, true); }
        }

        [Fact]
        public void Map_Content_IndentDepth_LessThanMinusOne_FallsBackWithDiagnostic()
        {
            var cfgDir = MakeTempDir();
            try
            {
                var dto = new ContextConfigDto
                {
                    Version = 1,
                    Content = new ContentDto
                    {
                        Enabled = true,
                        IndentDepth = -2 // invalid -> coerced to -1 + diag
                    }
                };

                var (_, _, content, _, diags) = ContextConfigMapper.Map(dto, null, cfgDir, null);
                Assert.Equal(-1, content.IndentDepth);
                Assert.Contains(diags, d => d.Contains("Invalid content.indentDepth", StringComparison.OrdinalIgnoreCase));
            }
            finally { Directory.Delete(cfgDir, true); }
        }

        [Fact]
        public void Map_Content_ContextPadding_Negative_ToZero_WithDiagnostic()
        {
            var cfgDir = MakeTempDir();
            try
            {
                var dto = new ContextConfigDto
                {
                    Version = 1,
                    Content = new ContentDto
                    {
                        Enabled = true,
                        ContextPadding = -1 // invalid -> coerced to 0 + diag
                    }
                };

                var (_, _, content, _, diags) = ContextConfigMapper.Map(dto, null, cfgDir, null);
                Assert.Equal(0, content.ContextPadding);
                Assert.Contains(diags, d => d.Contains("Invalid content.contextPadding", StringComparison.OrdinalIgnoreCase));
            }
            finally { Directory.Delete(cfgDir, true); }
        }

        [Fact]
        public void Map_Content_MaxFiles_Negative_Ignored_WithDiagnostic()
        {
            var cfgDir = MakeTempDir();
            try
            {
                var dto = new ContextConfigDto
                {
                    Version = 1,
                    Content = new ContentDto
                    {
                        Enabled = true,
                        MaxFiles = -5 // invalid -> null + diag
                    }
                };

                var (_, _, content, _, diags) = ContextConfigMapper.Map(dto, null, cfgDir, null);
                Assert.Null(content.MaxFiles);
                Assert.Contains(diags, d => d.Contains("Invalid content.maxFiles", StringComparison.OrdinalIgnoreCase));
            }
            finally { Directory.Delete(cfgDir, true); }
        }

        [Fact]
        public void Map_Content_Include_Normalization()
        {
            var cfgDir = MakeTempDir();
            try
            {
                var dto = new ContextConfigDto
                {
                    Version = 1,
                    Content = new ContentDto
                    {
                        Enabled = true,
                        Include = ["README", "*.md", "docs/"]
                    }
                };

                var (_, _, content, _, diags) = ContextConfigMapper.Map(dto, null, cfgDir, null);

                Assert.NotNull(content.Include);
                Assert.Contains("**/README", content.Include!);     // from "README"
                Assert.Contains("**/*.md", content.Include!);       // from "*.md"
                Assert.Contains("**/docs/**", content.Include!);    // from "docs/"

                Assert.Empty(diags); // normalization shouldn't emit warnings
            }
            finally { Directory.Delete(cfgDir, true); }
        }
    }
}