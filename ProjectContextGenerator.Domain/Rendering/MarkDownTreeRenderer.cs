using System.Text;
using ProjectContextGenerator.Domain.Abstractions;
using ProjectContextGenerator.Domain.Models;
using ProjectContextGenerator.Domain.Options;

namespace ProjectContextGenerator.Domain.Rendering
{
    /// <summary>
    /// Renders a directory tree to Markdown and, when enabled, embeds file contents
    /// beneath file nodes. Content rendering is language-agnostic and driven by indentation.
    /// </summary>
    public sealed class MarkdownTreeRenderer : ITreeRenderer
    {
        private const string Ellipsis = "...";

        private readonly IFileSystem? _fs;
        private readonly string _rootPath = "";
        private readonly ContentOptions _content = new();

        /// <summary>
        /// Default constructor for compatibility or tests that only require the tree layout.
        /// File content rendering is disabled in this mode.
        /// </summary>
        public MarkdownTreeRenderer() { }

        /// <summary>
        /// Constructs a Markdown renderer bound to a file system and scan root,
        /// with the provided content options.
        /// </summary>
        /// <param name="fs">File system used to read file contents.</param>
        /// <param name="rootPath">Absolute path of the scan root used to resolve file paths.</param>
        /// <param name="content">Options controlling file content rendering.</param>
        public MarkdownTreeRenderer(IFileSystem fs, string rootPath, ContentOptions content)
        {
            _fs = fs;
            _rootPath = rootPath;
            _content = content;
        }

        /// <inheritdoc />
        public string Render(DirectoryNode root)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"/{root.Name}/");
            RenderChildren(root.Children, level: 0, sb);
            return sb.ToString();
        }

        /// <summary>
        /// Recursively renders a list of nodes at a given indentation level.
        /// </summary>
        private void RenderChildren(IReadOnlyList<TreeNode> nodes, int level, StringBuilder sb)
        {
            foreach (var n in nodes)
            {
                var indent = new string(' ', level * 2);
                switch (n)
                {
                    case DirectoryNode d:
                        sb.AppendLine($"{indent}- {d.Name}/");
                        RenderChildren(d.Children, level + 1, sb);
                        break;

                    case FileNode f:
                        sb.AppendLine($"{indent}- {f.Name}");
                        if (_content.Enabled && _fs is not null && !string.IsNullOrEmpty(_rootPath) && f.RelativePath != "__ellipsis__")
                        {
                            RenderFileContentBlock(f, level + 1, sb);
                        }
                        break;
                }
            }
        }

        /// <summary>
        /// Renders the content of a single file under its bullet item, applying tab expansion,
        /// indentation filtering, optional line numbering, and line caps.
        /// </summary>
        private void RenderFileContentBlock(FileNode file, int level, StringBuilder sb)
        {
            var abs = System.IO.Path.Combine(_rootPath, file.RelativePath.Replace('/', System.IO.Path.DirectorySeparatorChar));

            string text;
            try
            {
                text = _fs!.ReadAllText(abs);
            }
            catch
            {
                AppendNoteLine(sb, level, "⟂ content not displayed (binary/unreadable)");
                return;
            }

            // Normalize EOLs
            text = text.Replace("\r\n", "\n").Replace('\r', '\n');

            // Determine tab width
            var tabWidth = _content.TabWidth;
            if (_content.DetectTabWidth)
            {
                var detected = DetectTabWidth(text, _content.TabWidth);
                if (detected is int dw && (dw == 2 || dw == 4 || dw == 8))
                    tabWidth = dw;
            }

            // Expand tabs
            var lines = text.Split('\n');
            for (int i = 0; i < lines.Length; i++)
                lines[i] = ExpandTabs(lines[i], tabWidth);

            // Filter + non-recursive padding, with ellipses
            var kept = FilterByIndentDepth(lines, _content.IndentDepth, tabWidth, _content.ContextPadding);

            // Per-file cap
            var truncated = false;
            if (_content.MaxLinesPerFile > 0 && kept.Count > _content.MaxLinesPerFile)
            {
                kept = [.. kept.Take(_content.MaxLinesPerFile)];
                truncated = true;
            }

            // Fenced code block nested under list item (indent fences only)
            var fenceIndent = new string(' ', level * 2) + "  ";
            sb.AppendLine($"{fenceIndent}```");

            int lineNo = 1;
            foreach (var line in kept)
            {
                // Do NOT prepend fenceIndent here; avoid injecting extra leading spaces into code
                if (_content.ShowLineNumbers)
                    sb.Append(lineNo).Append(": ").AppendLine(line);
                else
                    sb.AppendLine(line);

                lineNo++;
            }

            sb.AppendLine($"{fenceIndent}```");

            // Notes (aligned with list item)
            if (_content.IndentDepth >= 0)
                AppendNoteLine(sb, level, $"{Ellipsis} (lines deeper than level {_content.IndentDepth} hidden)");

            if (truncated)
                AppendNoteLine(sb, level, $"{Ellipsis} (truncated to {_content.MaxLinesPerFile} lines)");
        }

        /// <summary>
        /// Writes a short explanatory note aligned under the current bullet level.
        /// </summary>
        private static void AppendNoteLine(StringBuilder sb, int level, string note)
        {
            var indent = new string(' ', level * 2);
            sb.Append(indent).Append("  ").AppendLine(note);
        }

        private static string ExpandTabs(string s, int tabWidth)
        {
            if (string.IsNullOrEmpty(s) || s.IndexOf('\t') < 0) return s;

            var col = 0;
            var sb = new StringBuilder(s.Length + 8);

            foreach (var ch in s)
            {
                if (ch == '\t')
                {
                    var spaces = tabWidth - (col % tabWidth);
                    sb.Append(' ', spaces);
                    col += spaces;
                }
                else
                {
                    sb.Append(ch);
                    col += (ch == '\n') ? 0 : 1;
                }
            }

            return sb.ToString();
        }

        private static int? DetectTabWidth(string text, int fallback)
        {
            if (text.Contains('\t')) return fallback;

            var counts = new Dictionary<int, int>();
            var lines = text.Split('\n');
            int inspected = 0;

            foreach (var raw in lines)
            {
                if (inspected >= 200) break;
                if (string.IsNullOrWhiteSpace(raw)) continue;

                int leading = 0;
                while (leading < raw.Length && raw[leading] == ' ') leading++;

                if (leading > 0)
                {
                    counts[leading] = counts.TryGetValue(leading, out var c) ? c + 1 : 1;
                    inspected++;
                }
            }

            if (counts.Count == 0) return fallback;

            foreach (var candidate in new[] { 2, 4, 8 })
                if (counts.ContainsKey(candidate))
                    return candidate;

            return fallback;
        }

        /// <summary>
        /// Keeps lines whose indentation level (leadingSpaces / tabWidth) is within the configured depth.
        /// Adds non-recursive padding around core lines only, then collapses gaps with a single
        /// ellipsis line ("..."), indented one extra level (tabWidth * (indentDepth + 1)) for visual naturalness.
        /// </summary>
        private static List<string> FilterByIndentDepth(string[] lines, int indentDepth, int tabWidth, int contextPadding)
        {
            if (indentDepth < 0)
                return [.. lines];

            int n = lines.Length;

            // 1) Core lines by indentation threshold (blank lines are not core)
            var core = new bool[n];
            for (int i = 0; i < n; i++)
            {
                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                int leading = 0;
                while (leading < line.Length && line[leading] == ' ') leading++;
                int level = (tabWidth > 0) ? (leading / tabWidth) : 0;

                if (level <= indentDepth)
                    core[i] = true;
            }

            // 2) Non-recursive padding
            var kept = new bool[n];
            if (contextPadding <= 0)
            {
                for (int i = 0; i < n; i++) kept[i] = core[i];
            }
            else
            {
                for (int i = 0; i < n; i++)
                {
                    if (!core[i]) continue;
                    int from = Math.Max(0, i - contextPadding);
                    int to = Math.Min(n - 1, i + contextPadding);
                    for (int j = from; j <= to; j++) kept[j] = true;
                }
            }

            // 3) Intervals of kept lines
            var intervals = new List<(int Start, int End)>();
            int k = 0;
            while (k < n)
            {
                if (!kept[k]) { k++; continue; }
                int start = k;
                k++;
                while (k < n && kept[k]) k++;
                intervals.Add((start, k - 1));
            }

            // 4) Stitch with indented ellipsis between fragments
            var result = new List<string>(n);
            string ellipsisLine = new string(' ', Math.Max(0, (indentDepth + 1) * tabWidth)) + Ellipsis;

            for (int idx = 0; idx < intervals.Count; idx++)
            {
                var (start, end) = intervals[idx];

                if (idx > 0)
                    result.Add(ellipsisLine);

                for (int i = start; i <= end; i++)
                    result.Add(lines[i]);
            }

            return result;
        }
    }
}