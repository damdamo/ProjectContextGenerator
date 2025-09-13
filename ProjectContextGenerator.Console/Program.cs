using ProjectContextGenerator.Domain.Abstractions;
using ProjectContextGenerator.Domain.Models;
using ProjectContextGenerator.Domain.Options;
using ProjectContextGenerator.Domain.Rendering;
using ProjectContextGenerator.Domain.Services;
using ProjectContextGenerator.Infrastructure.FileSystem;
using ProjectContextGenerator.Infrastructure.Filtering;
using ProjectContextGenerator.Infrastructure.Globbing;
using ProjectContextGenerator.Infrastructure.GitIgnore;

// Composition root
IFileSystem fs = new SystemIOFileSystem();

// Options are the single source of truth
var options = new TreeScanOptions(
    MaxDepth: 3,
    IncludeGlobs: null, // include everything by default
    ExcludeGlobs: ["**/.git/**"],
    GitIgnore: GitIgnoreMode.RootOnly,       // or GitIgnoreMode.None to disable
    GitIgnoreFileName: ".gitignore"
);

// Build matchers from options
var includeMatcher = new GlobPathMatcher(options.IncludeGlobs, excludeGlobs: null);
var excludeMatcher = (options.ExcludeGlobs is { Count: > 0 })
    ? new GlobPathMatcher(includeGlobs: ["**/*"], excludeGlobs: options.ExcludeGlobs)
    : null;

// Load .gitignore according to options
var rootPath = @"C:\Damien\Dev\C#\ProjectContextGenerator\";
var ignoreProvider = new GitIgnoreRuleProvider(fs);
var ignoreRuleSet = options.GitIgnore == GitIgnoreMode.None
    ? EmptyIgnoreRuleSet.Instance
    : ignoreProvider.Load(rootPath, new IgnoreLoadingOptions(options.GitIgnore, options.GitIgnoreFileName ?? ".gitignore"));

// Compose final filter
IPathFilter filter = new CompositePathFilter(includeMatcher, excludeMatcher, ignoreRuleSet);

// Build + render
ITreeBuilder builder = new TreeBuilder(fs, filter);
ITreeRenderer renderer = new MarkdownTreeRenderer();

var tree = builder.Build(rootPath, options);
var output = renderer.Render(tree);
Console.WriteLine(output);
