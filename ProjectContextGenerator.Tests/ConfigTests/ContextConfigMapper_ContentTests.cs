using System;
using System.Collections.Generic;
using System.IO;
using ProjectContextGenerator.Domain.Config;
using Xunit;

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
    }
}