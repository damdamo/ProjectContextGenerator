namespace ProjectContextGenerator.Domain.Options
{
    /// <summary>
    /// Options controlling how file contents are rendered beneath file nodes.
    /// This is language-agnostic and relies primarily on indentation depth.
    /// </summary>
    /// <param name="Enabled">
    /// When true, file contents are rendered beneath each matching file node.
    /// </param>
    /// <param name="IndentDepth">
    /// Maximum indentation depth to keep (0 = top-level, 1 = one extra level, etc.).
    /// Use -1 to keep all depths.
    /// </param>
    /// <param name="TabWidth">
    /// Number of spaces a tab represents when expanding tabs into spaces.
    /// Typical values are 2, 4, or 8.
    /// </param>
    /// <param name="DetectTabWidth">
    /// When true, attempts a lightweight auto-detection of typical indentation width
    /// on the first lines of the file; falls back to <see cref="TabWidth"/> if none is found.
    /// </param>
    /// <param name="MaxLinesPerFile">
    /// Maximum number of lines to render per file after indentation filtering.
    /// Use -1 for unlimited.
    /// </param>
    /// <param name="ShowLineNumbers">
    /// When true, prepends line numbers to rendered content lines for orientation.
    /// </param>
    /// <param name="ContextPadding">
    /// Number of lines of context to keep around retained lines to preserve readability.
    /// </param>
    /// <param name="MaxFiles">
    /// Optional global cap on the number of files for which content will be rendered.
    /// Null means no global cap.
    /// </param>
    /// <param name="Include">
    /// Glob patterns selecting which visible files should have their content rendered.
    /// If null or empty, no file content is rendered even when <see cref="Enabled"/> is true.
    /// </param>
    public sealed record ContentOptions(
        bool Enabled = false,
        int IndentDepth = 1,
        int TabWidth = 4,
        bool DetectTabWidth = true,
        int MaxLinesPerFile = 300,
        bool ShowLineNumbers = false,
        int ContextPadding = 1,
        int? MaxFiles = null,
        IReadOnlyList<string>? Include = null
    );
}
