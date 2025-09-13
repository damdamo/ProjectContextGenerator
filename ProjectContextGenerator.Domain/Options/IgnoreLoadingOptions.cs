namespace ProjectContextGenerator.Domain.Options
{
    /// <summary>
    /// Options influencing how ignore rules are discovered and compiled.
    /// </summary>
    /// <param name="Mode">Which .gitignore strategy to use (None, RootOnly, Nested).</param>
    /// <param name="GitIgnoreFileName">
    /// The filename to look for at the root (defaults to ".gitignore"). This is useful for testing.
    /// </param>
    public sealed record IgnoreLoadingOptions(
        GitIgnoreMode Mode = GitIgnoreMode.RootOnly,
        string GitIgnoreFileName = ".gitignore"
    );
}