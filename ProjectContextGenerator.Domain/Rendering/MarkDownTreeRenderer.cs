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
            // Compose absolute path using the scan root.
            var abs = System.IO.Path.Combine(_rootPath, file.RelativePath.Replace('/', System.IO.Path.DirectorySeparatorChar));

            string text;
            try
            {
                text = _fs!.ReadAllText(abs);
            }
            catch
            {
                // Do not fail the entire rendering if one file is unreadable.
                AppendNoteLine(sb, level, "⟂ content not displayed (binary/unreadable)");
                return;
            }

            // Normalize line endings. Keep content as-is otherwise (language-agnostic).
            text = text.Replace("\r\n", "\n").Replace('\r', '\n');

            // Determine tab width (either auto-detected or the configured fallback).
            var tabWidth = _content.TabWidth;
            if (_content.DetectTabWidth)
            {
                var detected = DetectTabWidth(text, _content.TabWidth);
                if (detected is int dw && (dw == 2 || dw == 4 || dw == 8))
                    tabWidth = dw;
            }

            // Expand tabs to spaces to get a stable indentation measure.
            var lines = text.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                lines[i] = ExpandTabs(lines[i], tabWidth);
            }

            // Keep only lines whose indentation level <= IndentDepth, with padding context around them.
            var kept = FilterByIndentDepth(lines, _content.IndentDepth, tabWidth, _content.ContextPadding);

            // Enforce per-file cap after filtering.
            var truncated = false;
            if (_content.MaxLinesPerFile > 0 && kept.Count > _content.MaxLinesPerFile)
            {
                kept = [.. kept.Take(_content.MaxLinesPerFile)];
                truncated = true;
            }

            // Render as a fenced code block under the file bullet.
            var bulletIndent = new string(' ', level * 2);
            sb.AppendLine($"{bulletIndent}  ```");
            int lineNo = 1;
            foreach (var line in kept)
            {
                if (_content.ShowLineNumbers)
                    sb.Append(bulletIndent).Append("  ").Append(lineNo).Append(": ").AppendLine(line);
                else
                    sb.Append(bulletIndent).Append("  ").AppendLine(line);

                lineNo++;
            }
            sb.AppendLine($"{bulletIndent}  ```");

            // Informative elision markers (only when depth limiting is active).
            if (_content.IndentDepth >= 0)
                AppendNoteLine(sb, level, $"… (lines deeper than level {_content.IndentDepth} hidden)");

            if (truncated)
                AppendNoteLine(sb, level, $"… (truncated to {_content.MaxLinesPerFile} lines)");
        }

        /// <summary>
        /// Writes a short explanatory note aligned under the current bullet level.
        /// </summary>
        private static void AppendNoteLine(StringBuilder sb, int level, string note)
        {
            var indent = new string(' ', level * 2);
            sb.Append(indent).Append("  ").AppendLine(note);
        }

        /// <summary>
        /// Expands tabs to spaces using the provided tab width, preserving visual alignment.
        /// </summary>
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
                    // We compute column width on a per-character basis; this is sufficient for indentation count.
                    col += (ch == '\n') ? 0 : 1;
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Attempts a lightweight tab width detection by scanning indent patterns in the first lines.
        /// Falls back to <paramref name="fallback"/> if no reliable signal is found.
        /// </summary>
        private static int? DetectTabWidth(string text, int fallback)
        {
            // If the file contains literal tabs, we simply use the fallback width.
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

            // Prefer small, common multiples typical in codebases: 2, 4, then 8.
            foreach (var candidate in new[] { 2, 4, 8 })
            {
                if (counts.ContainsKey(candidate))
                    return candidate;
            }

            return fallback;
        }

        /// <summary>
        /// Keeps lines whose indentation level (computed as leadingSpaces / tabWidth) is within the configured depth.
        /// Adds padding lines around kept lines to preserve local context, and collapses large gaps with an ellipsis line.
        /// </summary>
        private static List<string> FilterByIndentDepth(string[] lines, int indentDepth, int tabWidth, int contextPadding)
        {
            if (indentDepth < 0)
                return [.. lines];

            var keepMask = new bool[lines.Length];

            // Mark lines to keep based on indentation threshold.
            for (int i = 0; i < lines.Length; i++)
            {
                int leading = 0;
                var line = lines[i];
                while (leading < line.Length && line[leading] == ' ') leading++;
                int level = (tabWidth > 0) ? (leading / tabWidth) : 0;
                if (level <= indentDepth) keepMask[i] = true;
            }

            // Add context around kept lines to avoid isolated fragments.
            if (contextPadding > 0)
            {
                for (int i = 0; i < lines.Length; i++)
                {
                    if (!keepMask[i]) continue;
                    int from = Math.Max(0, i - contextPadding);
                    int to = Math.Min(lines.Length - 1, i + contextPadding);
                    for (int j = from; j <= to; j++) keepMask[j] = true;
                }
            }

            // Build final list, collapsing large gaps into a visible ellipsis line.
            var result = new List<string>(lines.Length);
            bool inGap = false;

            for (int i = 0; i < lines.Length; i++)
            {
                if (keepMask[i])
                {
                    if (inGap)
                    {
                        result.Add("…"); // single visual marker for a skipped block
                        inGap = false;
                    }
                    result.Add(lines[i]);
                }
                else
                {
                    inGap = true;
                }
            }

            return result;
        }
    }
}