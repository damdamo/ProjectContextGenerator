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
    /// <param name="Enabled">
    /// When true, the history block is enabled (a value of <see cref="Last"/> &gt; 0 is still required to render).
    /// Default: true for backward compatibility.
    /// </param>
    /// <param name="Last">Number of commits to include (0 disables history rendering).</param>
    /// <param name="MaxBodyLines">Maximum number of body lines per commit after trimming empties.</param>
    /// <param name="Detail">History detail level: titles only or title+body.</param>
    /// <param name="IncludeMerges">Include merge commits if true.</param>
    public sealed record HistoryOptions(
        bool Enabled = true,
        int Last = 20,
        int MaxBodyLines = 6,
        HistoryDetail Detail = HistoryDetail.TitlesOnly,
        bool IncludeMerges = false
    );
}
