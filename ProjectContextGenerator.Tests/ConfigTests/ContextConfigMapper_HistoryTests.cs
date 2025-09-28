using ProjectContextGenerator.Domain.Config;
using ProjectContextGenerator.Domain.Options;

namespace ProjectContextGenerator.Tests.ConfigTests
{
    public sealed class ContextConfigMapper_HistoryTests
    {
        private static string MakeTempDir()
        {
            var dir = Path.Combine(Path.GetTempPath(), "treecfg_hist_", Guid.NewGuid().ToString("N"));
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
                var (_, history, _, diags) = ContextConfigMapper.Map(dto, null, cfgDir, null);

                Assert.Empty(diags);
                Assert.Equal(20, history.Last);
                Assert.Equal(6, history.MaxBodyLines);
                Assert.Equal(HistoryDetail.TitlesOnly, history.Detail);
                Assert.False(history.IncludeMerges);
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
                        MaxBodyLines = 2,
                        Detail = "TitleAndBody",
                        IncludeMerges = true
                    }
                }
                ;
                var (_, h, _, diags) = ContextConfigMapper.Map(dto, null, cfgDir, null);
                Assert.Empty(diags);
                Assert.Equal(7, h.Last);
                Assert.Equal(2, h.MaxBodyLines);
                Assert.Equal(HistoryDetail.TitleAndBody, h.Detail);
                Assert.True(h.IncludeMerges);
            }
            finally { Directory.Delete(cfgDir, true); }
        }

        [Fact]
        public void History_DeepMerge_Profile_Overrides_Field_By_Field()
        {
            var cfgDir = MakeTempDir();
            try
            {
                var dto = new ContextConfigDto
                {
                    Version = 1,
                    History = new HistoryDto { Last = 20, MaxBodyLines = 6, Detail = "TitlesOnly", IncludeMerges = false },
                    Profiles = new Dictionary<string, ContextConfigDto>
                    {
                        ["full"] = new()
                        {
                            History = new HistoryDto { Detail = "TitleAndBody" } // override only detail
                        }
                    }
                }
                ;
                var (_, h, _, diags) = ContextConfigMapper.Map(dto, "full", cfgDir, null);
                Assert.Empty(diags);
                Assert.Equal(20, h.Last);
                Assert.Equal(6, h.MaxBodyLines);
                Assert.Equal(HistoryDetail.TitleAndBody, h.Detail);
                Assert.False(h.IncludeMerges);
            }
            finally { Directory.Delete(cfgDir, true); }
        }

        [Fact]
        public void History_Invalid_Values_Produce_Diagnostics_And_Fallbacks()
        {
            var cfgDir = MakeTempDir();
            try
            {
                var dto = new ContextConfigDto
                {
                    Version = 1,
                    History = new HistoryDto
                    {
                        Last = -5,               // invalid
                        MaxBodyLines = -1,       // invalid
                        Detail = "NotAValue"     // invalid
                    }
                }
                ;
                var (_, h, _, diags) = ContextConfigMapper.Map(dto, null, cfgDir, null);
                Assert.Equal(0, h.Last);
                Assert.Equal(0, h.MaxBodyLines);
                Assert.Equal(HistoryDetail.TitlesOnly, h.Detail);
                Assert.Contains(diags, d => d.Contains("Invalid history.last", StringComparison.OrdinalIgnoreCase));
                Assert.Contains(diags, d => d.Contains("Invalid history.maxBodyLines", StringComparison.OrdinalIgnoreCase));
                Assert.Contains(diags, d => d.Contains("Unknown history.detail", StringComparison.OrdinalIgnoreCase));
            }
            finally { Directory.Delete(cfgDir, true); }
        }
    }
}
