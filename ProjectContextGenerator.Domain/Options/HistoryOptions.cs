using ProjectContextGenerator.Domain.Options;

namespace ProjectContextGenerator.Domain.Options
{
    public enum HistoryDetail
    {
        TitlesOnly = 0,
        TitleAndBody = 1
    }

    /// <summary>
    /// Options that control how recent Git history is collected and rendered.
    /// </summary>
    public sealed record HistoryOptions(
        int Last = 20,
        int MaxBodyLines = 6,
        HistoryDetail Detail = HistoryDetail.TitlesOnly,
        bool IncludeMerges = false
    );
}
