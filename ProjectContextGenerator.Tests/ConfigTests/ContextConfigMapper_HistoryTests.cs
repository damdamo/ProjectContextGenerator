using System;
using System.IO;
using ProjectContextGenerator.Domain.Config;
using ProjectContextGenerator.Domain.Options;
using Xunit;

namespace ProjectContextGenerator.Tests.ConfigTests
{
    public sealed class ContextConfigMapper_HistoryTests
    {
        private static string MakeTempDir()
        {
            var dir = Path.Combine(Path.GetTempPath(), "treecfg_histtests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        [Fact]
        public void History_Defaults_WhenMissing()
        {
            var cfgDir = MakeTempDir();
            try
            {
                var dto = new ContextConfigDto { Version = 1 };

                // Map returns: (Options, History, Content, Root, Diagnostics)
                var (_, history, _, _, diags) = ContextConfigMapper.Map(dto, null, cfgDir, null);

                Assert.Equal(10, history.Last);
                Assert.Equal(6, history.MaxBodyLines);
                Assert.Equal(HistoryDetail.TitlesOnly, history.Detail);
                Assert.False(history.IncludeMerges);
                Assert.Empty(diags);
            }
            finally { Directory.Delete(cfgDir, true); }
        }

        [Fact]
        public void History_Maps_All_Fields()
        {
            var cfgDir = MakeTempDir();
            try
            {
                var dto = new ContextConfigDto
                {
                    Version = 1,
                    History = new HistoryDto
                    {
                        Last = 7,
                        MaxBodyLines = 3,
                        Detail = "TitleAndBody",
                        IncludeMerges = true
                    }
                };

                var (_, history, _, _, diags) = ContextConfigMapper.Map(dto, null, cfgDir, null);

                Assert.Equal(7, history.Last);
                Assert.Equal(3, history.MaxBodyLines);
                Assert.Equal(HistoryDetail.TitleAndBody, history.Detail);
                Assert.True(history.IncludeMerges);
                Assert.Empty(diags);
            }
            finally { Directory.Delete(cfgDir, true); }
        }

        [Fact]
        public void History_Validation_Errors_For_Invalid_Values()
        {
            var cfgDir = MakeTempDir();
            try
            {
                var dto = new ContextConfigDto
                {
                    Version = 1,
                    History = new HistoryDto
                    {
                        Last = -5,              // invalid -> coerced to 0
                        MaxBodyLines = -2,      // invalid -> coerced to 0
                        Detail = "SomethingElse"// invalid -> fallback TitlesOnly
                    }
                };

                var (_, history, _, _, diags) = ContextConfigMapper.Map(dto, null, cfgDir, null);

                Assert.Equal(0, history.Last);
                Assert.Equal(0, history.MaxBodyLines);
                Assert.Equal(HistoryDetail.TitlesOnly, history.Detail);

                // Diagnostics messages come from ContextConfigMapper.BuildHistoryOptions:
                // - "Invalid history.last '{last}'. Using 0."
                // - "Invalid history.maxBodyLines '{maxBody}'. Using 0."
                // - "Unknown history.detail '{detailStr}'. Falling back to TitlesOnly."
                Assert.Contains(diags, d => d.Contains("Invalid history.last", StringComparison.OrdinalIgnoreCase));
                Assert.Contains(diags, d => d.Contains("Invalid history.maxBodyLines", StringComparison.OrdinalIgnoreCase));
                Assert.Contains(diags, d => d.Contains("Unknown history.detail", StringComparison.OrdinalIgnoreCase));
            }
            finally { Directory.Delete(cfgDir, true); }
        }

        [Fact]
        public void History_Profile_Overrides_Root()
        {
            var cfgDir = MakeTempDir();
            try
            {
                var dto = new ContextConfigDto
                {
                    Version = 1,
                    History = new HistoryDto { Last = 5, Detail = "TitlesOnly", IncludeMerges = false, MaxBodyLines = 2 },
                    Profiles = new System.Collections.Generic.Dictionary<string, ContextConfigDto>
                    {
                        ["full"] = new ContextConfigDto
                        {
                            History = new HistoryDto { Last = 12, Detail = "TitleAndBody", IncludeMerges = true, MaxBodyLines = 8 }
                        }
                    }
                };

                var (_, history, _, _, diags) = ContextConfigMapper.Map(dto, "full", cfgDir, null);

                Assert.Equal(12, history.Last);
                Assert.Equal(8, history.MaxBodyLines);
                Assert.Equal(HistoryDetail.TitleAndBody, history.Detail);
                Assert.True(history.IncludeMerges);
                Assert.Empty(diags);
            }
            finally { Directory.Delete(cfgDir, true); }
        }

        [Fact]
        public void Map_History_Defaults_WhenBlockMissing()
        {
            var cfgDir = MakeTempDir();
            try
            {
                var dto = new ContextConfigDto { Version = 1, History = null };
                var (_, history, _, _, diags) = ContextConfigMapper.Map(dto, null, cfgDir, null);

                Assert.False(history.Enabled);                      // policy default
                Assert.Equal(10, history.Last);                     // policy default
                Assert.Equal(HistoryDetail.TitlesOnly, history.Detail);
                Assert.Equal(6, history.MaxBodyLines);
                Assert.False(history.IncludeMerges);
                Assert.Empty(diags);
            }
            finally { Directory.Delete(cfgDir, true); }
        }

        [Fact]
        public void Map_History_Validation_And_Diagnostics()
        {
            var cfgDir = MakeTempDir();
            try
            {
                var dto = new ContextConfigDto
                {
                    Version = 1,
                    History = new HistoryDto
                    {
                        Enabled = true,
                        Last = -5,               // invalid -> coerced to 0 + diag
                        MaxBodyLines = -1,       // invalid -> coerced to 0 + diag
                        Detail = "WeirdDetail",  // unknown -> TitlesOnly + diag
                        IncludeMerges = true
                    }
                };

                var (_, history, _, _, diags) = ContextConfigMapper.Map(dto, null, cfgDir, null);

                Assert.True(history.Enabled);
                Assert.Equal(0, history.Last);
                Assert.Equal(0, history.MaxBodyLines);
                Assert.Equal(HistoryDetail.TitlesOnly, history.Detail);
                Assert.True(history.IncludeMerges);

                Assert.Contains(diags, d => d.Contains("Invalid history.last", StringComparison.OrdinalIgnoreCase));
                Assert.Contains(diags, d => d.Contains("Invalid history.maxBodyLines", StringComparison.OrdinalIgnoreCase));
                Assert.Contains(diags, d => d.Contains("Unknown history.detail", StringComparison.OrdinalIgnoreCase));
            }
            finally { Directory.Delete(cfgDir, true); }
        }
    }
}