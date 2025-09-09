using ProjectContextGenerator.Domain.Abstractions;
using ProjectContextGenerator.Domain.Models;
using ProjectContextGenerator.Domain.Services;
using ProjectContextGenerator.Domain.Rendering;
using ProjectContextGenerator.Domain.Options;
using ProjectContextGenerator.Infrastructure;

IFileSystem fs = new SystemIOFileSystem();
IPathMatcher matcher = new GlobPathMatcher(
    includeGlobs: null,
    excludeGlobs: ["**/bin/**", "**/obj/**", "**/.git/**", "**/.vs/**", "**/node_modules/**"]
);

ITreeBuilder builder = new TreeBuilder(fs, matcher);
ITreeRenderer renderer = new MarkdownTreeRenderer();

var options = new TreeScanOptions(
    MaxDepth: 4,
    ExcludeGlobs: ["**/bin/**", "**/obj/**", "**/node_modules/**", "**/.git/**"],
    IncludeGlobs: null
);

var tree = builder.Build(@"C:\Damien\Dev\C#\ProjectContextGenerator\ProjectContextGenerator.Domain", options);
var output = renderer.Render(tree);
Console.WriteLine(output);
