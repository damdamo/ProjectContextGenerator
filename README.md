# ProjectContextGenerator

Generate a clean, shareable tree of your repository (Markdown, plain text, JSON) with precise filtering that honors both **glob patterns** and **.gitignore** semantics. Perfect for pasting into PRs, issues, or LLM prompts without manual cleanup.

---

## Highlights

- **Multiple renderers:** Markdown / Plain text.
- **Filtering:** Include/Exclude **globs** + `.gitignore` (Root-only or Nested).
- **Clean architecture:** Domain vs Infrastructure separation; testable components; fake filesystem for deterministic tests.
- **CI-ready:** GitHub Actions workflow included (`.github/workflows/ci.yml`).

---

## Project Layout

```
/ProjectContextGenerator
├─ .github/workflows/
│  └─ ci.yml
├─ ProjectContextGenerator.Console/
├─ ProjectContextGenerator.Domain/
│  ├─ Abstractions/
│  ├─ Models/
│  ├─ Options/
│  ├─ Rendering/
│  ├─ Services/
│  └─ ProjectContextGenerator.Domain.csproj
├─ ProjectContextGenerator.Infrastructure/
│  ├─ FileSystem/
│  ├─ Filtering/
│  ├─ GitIgnore/
│  ├─ Globbing/
│  └─ ProjectContextGenerator.Infrastructure.csproj
├─ ProjectContextGenerator.Tests/
│  ├─ Fakes/
│  ├─ GlobbingTests/
│  ├─ RenderingTests/
│  └─ TreeBuilderTests/
└─ ProjectContextGenerator.sln
```

---

## Requirements

- **.NET 8 SDK**

---

## Quickstart

```bash
# Build
dotnet build

# Run tests
dotnet test

# Run the console sample (prints a Markdown tree)
dotnet run --project ProjectContextGenerator.Console
```

---

## Usage

### Compose filtering once, then build & render

```csharp
using ProjectContextGenerator.Domain.Abstractions;
using ProjectContextGenerator.Domain.Options;
using ProjectContextGenerator.Domain.Rendering;
using ProjectContextGenerator.Domain.Services;
using ProjectContextGenerator.Infrastructure.FileSystem;
using ProjectContextGenerator.Infrastructure.Filtering;
using ProjectContextGenerator.Infrastructure.Globbing;
using ProjectContextGenerator.Infrastructure.GitIgnore;

// File system
IFileSystem fs = new SystemIOFileSystem();

// Options: single source of truth for traversal and defaults
var options = new TreeScanOptions(
    MaxDepth: 4,
    IncludeGlobs: null,  // include everything by default
    ExcludeGlobs: ["**/.git/**", "**/.vs/**", "**/bin/**", "**/obj/**", "**/node_modules/**"],
    GitIgnore: GitIgnoreMode.Nested,            // or RootOnly / None
    GitIgnoreFileName: ".gitignore"
);

var rootPath = @"C:\path\to\your\repo";

// Build globs
var includeMatcher = new GlobPathMatcher(options.IncludeGlobs, excludeGlobs: null);
var excludeMatcher = (options.ExcludeGlobs is { Count: > 0 })
    ? new GlobPathMatcher(includeGlobs: ["**/*"], excludeGlobs: options.ExcludeGlobs)
    : null;

// Load .gitignore rules (RootOnly or Nested)
var ignoreRuleSet = (options.GitIgnore == GitIgnoreMode.None)
    ? EmptyIgnoreRuleSet.Instance
    : new GitIgnoreRuleProvider(fs).Load(
        rootPath,
        new IgnoreLoadingOptions(options.GitIgnore, options.GitIgnoreFileName ?? ".gitignore")
      );

// Compose the policy used by TreeBuilder
IPathFilter filter = new CompositePathFilter(includeMatcher, excludeMatcher, ignoreRuleSet);

// Build + render
ITreeBuilder builder = new TreeBuilder(fs, filter);
ITreeRenderer renderer = new MarkdownTreeRenderer();

var tree = builder.Build(rootPath, options);
Console.WriteLine(renderer.Render(tree));
```

### Options overview

- `MaxDepth`: `0` = root only; `1` = root + direct children; `-1` = unlimited.
- `IncludeGlobs` / `ExcludeGlobs`: optional glob lists (e.g., `["**/*.cs"]`, `["**/bin/**"]`).
- `SortDirectoriesFirst`: default `true`.
- `CollapseSingleChildDirectories`: default `true` (render `a/b/c/` chain as one path).
- `MaxItemsPerDirectory`: optional cap with placeholder node (`… (+N more)`).
- `GitIgnore`: `None` | `RootOnly` | `Nested`.
- `GitIgnoreFileName`: typically `".gitignore"`.

### Filtering precedence

1. **Include globs** must match.  
2. **.gitignore** must *not* ignore the path.  
3. **Exclude globs** must *not* exclude the path.  

A path is included only if **all three** checks pass.

### .gitignore semantics (implemented)

- **Last matching rule wins** (we evaluate in reverse order for speed).
- **Directory-only** patterns (`pattern/`) match the directory **and its subtree**.
- **Anchored** patterns (`/pattern`) are anchored to the **scope**: repo root in RootOnly mode, containing directory for Nested mode.
- **Character classes** supported: `[abc]`, ranges `[a-z]`, and negation `[!abc]`.
- **Parent un-ignore required**: you cannot re-include a file if any parent directory is excluded; unignore parents first (e.g., `!bin/` then `!bin/keep/`).

> Note: `.git/` is not typically listed in `.gitignore`. Exclude VCS directories via `ExcludeGlobs` if you don’t want them in the tree.

---

## Renderers

- **Markdown**: human-friendly tree (folders end with `/`).
- **PlainText**: simple ASCII output.
- **JSON**: machine-friendly representation of `DirectoryNode` / `FileNode` trees.

---

## Testing

- Deterministic unit tests using `FakeFileSystem`.
- Coverage includes globbing, tree building behavior, renderer stability, and `.gitignore` (RootOnly & Nested).

Run:
```bash
dotnet test
```

---

## Contributing

Contributions are welcome. Please:
- Keep public APIs documented (XML comments).
- Add tests for new behavior.
- Follow the folder/namespace conventions.

---

## License

Licensed under the **Apache License 2.0**. See `LICENSE.txt` for details.
