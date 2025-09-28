# ProjectContextGenerator

Generate a clean, shareable context of your repository in Markdown, with:

- A filtered directory tree (glob patterns + .gitignore).
- Optional file content excerpts controlled by indentation-based filtering.
- Recent Git commit history (titles or titles+body).

Filtering honors **glob patterns**, **.gitignore semantics**, and
optional **config profiles**.\
Perfect for pasting into PRs, issues, or LLM prompts to provide rich
project context without manual cleanup.

------------------------------------------------------------------------

## Highlights

-   **Filtering:** Include/Exclude **globs** + `.gitignore` (Root-only
    or Nested).
-   **Config profiles:** Define reusable presets in `.contextgen.json`
    and select them at runtime.
-   **Flexible modes:** Combine `DirectoriesOnly` with include patterns
    to list only the directory skeleton for specific file types
    (e.g. "all folders that contain JSON files").
-   **History mode:** Optionally append recent Git commit messages
    (titles or titles+body) after the tree for extra project context.
-   **Content mode:** Render file contents under file entries,
    language-agnostic, guided by **indentation depth**, with optional
    tab width detection, per-file line caps, and context padding.

------------------------------------------------------------------------

## Project Layout

    /ProjectContextGenerator
    ├─ ProjectContextGenerator.Console/
    │  └─ Properties/
    ├─ ProjectContextGenerator.Domain/
    │  ├─ Abstractions/
    │  ├─ Config/
    │  ├─ Models/
    │  ├─ Options/
    │  ├─ Rendering/
    │  ├─ Services/
    ├─ ProjectContextGenerator.Infrastructure/
    │  ├─ FileSystem/
    │  ├─ Filtering/
    │  ├─ GitIgnore/
    │  ├─ Globbing/
    ├─ ProjectContextGenerator.Tests/
    │  ├─ ConfigTests/
    │  ├─ Fakes/
    │  ├─ GlobbingTests/
    │  ├─ RenderingTests/
    │  ├─ TreeBuilderTests/
    └─ ProjectContextGenerator.sln

------------------------------------------------------------------------

## Requirements

-   **.NET 8 SDK**
-   **Git** (in `PATH`) if using the `history` feature.

------------------------------------------------------------------------

## Quickstart

``` bash
# Build
dotnet build

# Run tests
dotnet test

# Run the console sample
dotnet run --project /path/to/your/project
```

------------------------------------------------------------------------

## Configuration

Tree generation is driven by a JSON config file, typically named
`.contextgen.json`.\
The console automatically picks it up from the working directory unless
overridden with `--config`.

Example:

``` jsonc
{
  "version": 1,
  "root": ".",
  "maxDepth": 3,
  "exclude": ["bin/", "obj/", ".git/", "node_modules/"],
  "gitIgnore": "Nested",

  "content": {
    "enabled": false,
    "indentDepth": 1,
    "tabWidth": 4,
    "detectTabWidth": true,
    "maxLinesPerFile": 300,
    "showLineNumbers": false,
    "contextPadding": 1
  },

  "history": {
    "enabled": true,
    "last": 20,
    "maxBodyLines": 6,
    "detail": "TitlesOnly",
    "includeMerges": false
  },

  "profiles": {
  "fast": {
    "maxDepth": 3,
    "directoriesOnly": true,
    "content": {
      "enabled": false
    },
    "history": {
      "enabled": false
    }
  },
  "full": {
    "maxDepth": -1,
    "collapseSingleChildDirectories": false,
    "history": {
      "detail": "TitleAndBody"
    },
    "content": {
      "enabled": true,
      "indentDepth": 1 // Show full content (no indentation filtering)
    }
  }
}
```

### Profiles

Profiles are partial configs that override the root config when
selected.\
`history` and **`content`** settings can also be overridden per-profile.

``` bash
dotnet run --project ProjectContextGenerator.Console --profile fast
dotnet run --project ProjectContextGenerator.Console --profile csharp
dotnet run --project ProjectContextGenerator.Console --profile full
```

### Root resolution

Root is resolved in this order of precedence:

1.  `--root` (CLI argument)\
2.  `root` field in config or profile\
3.  Default `"."` (current directory)

Relative paths are resolved against either the CLI working directory or
the config file's location.

------------------------------------------------------------------------

## Usage

### Console

``` bash
# Default: uses ./.contextgen.json if present, and an optional profile
dotnet run --project ProjectContextGenerator.Console --profile full

# Explicit config path
dotnet run --project ProjectContextGenerator.Console --config ./configs/custom.json

# Override root at runtime
dotnet run --project ProjectContextGenerator.Console --root ../other-repo
```

------------------------------------------------------------------------

## Options overview

-   `MaxDepth`: `0` = root only; `1` = root + direct children; `-1` =
    unlimited.
-   `IncludeGlobs` / `ExcludeGlobs`: optional glob lists (e.g.,
    `["**/*.cs"]`, `["**/bin/**"]`).
-   `SortDirectoriesFirst`: default `true`.
-   `CollapseSingleChildDirectories`: default `true`.
-   `MaxItemsPerDirectory`: optional cap with placeholder node
    (`… (+N more)`).
-   `GitIgnore`: `None` \| `RootOnly` \| `Nested`.
-   `GitIgnoreFileName`: typically `".gitignore"`.
-   `DirectoriesOnly`: default `false`.

------------------------------------------------------------------------

## Content

**What it does:**\
Embeds **file content excerpts** directly under each file node in the
Markdown tree.\
This is **language-agnostic** and driven by **indentation depth**, with
optional tab width detection.

**Why indentation?**\
For most code, indentation correlates with scope and importance. Smaller
indentation = higher-level constructs (e.g., package/namespace,
module/class signatures).\
By choosing `indentDepth`, you can surface just the top-level structure,
go one level deeper (class/method signatures), or include everything.

### Content options

-   `Enabled` (`bool`, default `false`): turn content rendering on/off.
-   `IndentDepth` (`int`, default `1`): keep lines whose indent level ≤
    this value.\
    Use `-1` to keep **all** depths (full content).
-   `TabWidth` (`int`, default `4`): how many spaces a tab represents
    when expanding tabs.
-   `DetectTabWidth` (`bool`, default `true`): lightweight
    auto-detection for common indentation widths (2/4/8); falls back to
    `TabWidth`.
-   `MaxLinesPerFile` (`int`, default `300`): cap lines per file after
    filtering. Use `-1` for unlimited.
-   `ShowLineNumbers` (`bool`, default `false`): prefix rendered content
    lines with line numbers.
-   `ContextPadding` (`int`, default `1`): number of extra lines to keep
    around retained lines to preserve readability.
-   `MaxFiles` (`int?`, default `null`): optional global cap on number
    of files with rendered content.

> Notes: - Tabs are expanded to spaces before computing indentation
> levels.\
> - Files are read as text (UTF-8). Unreadable/binary files show a short
> explanatory marker instead of failing the run.\
> - The internal tree model stores **relative paths** so content can be
> resolved robustly regardless of the absolute machine path.

### Example output (tree + content)

```` markdown
/ProjectContextGenerator/
- ProjectContextGenerator.Domain/
  - Options/
    - ContentOptions.cs
      ```
      namespace ProjectContextGenerator.Domain.Options
      {
          public sealed record ContentOptions(
              bool Enabled = false,
              int IndentDepth = 1,
              int TabWidth = 4,
              bool DetectTabWidth = true,
              int MaxLinesPerFile = 300,
              bool ShowLineNumbers = false,
              int ContextPadding = 1,
              int? MaxFiles = null
          );
      }
      ```
      … (lines deeper than level 1 hidden)
      … (truncated to 300 lines)
````

### Practical presets

-   **Overview**: `indentDepth = 0` (top-level only),
    `contextPadding = 0`
-   **Signatures**: `indentDepth = 1 or 2`, `contextPadding = 1`
-   **Full**: `indentDepth = -1`, `maxLinesPerFile = -1`

------------------------------------------------------------------------

## History

When enabled, a block of recent Git commits is appended after the tree
output. This is useful to give additional context about the latest
changes in a repository.

### History options

-	`Enabled`: when `true`, enables the history block (requires `Last > 0` to render). Default `true`.
-   `Last`: number of commits to show. Default `20`. `0` disables
    history.
-   `MaxBodyLines`: max number of body lines per commit (after trimming
    empties). Default `6`.
-   `Detail`:
    -   `"TitlesOnly"` (default): only show commit titles.
    -   `"TitleAndBody"`: show title and body lines (indented).
-   `IncludeMerges`: if `true`, merge commits are included. Default
    `false`.

### Example output

``` markdown
## Recent Changes (last 5)
- feat: add history support
  parses commits using git log
  renders Markdown
- fix: handle Windows line endings
```

------------------------------------------------------------------------

## Filtering precedence

1.  **Include globs** must match (if provided).\
2.  **.gitignore** must *not* ignore the path.\
3.  **Exclude globs** must *not* exclude the path.

A path is included only if **all three** checks pass.

------------------------------------------------------------------------

## .gitignore semantics

-   **Last matching rule wins**.\
-   **Directory-only patterns** (`pattern/`) match the directory **and
    its subtree**.\
-   **Anchored** patterns (`/pattern`) relative to repo root (RootOnly)
    or containing folder (Nested).\
-   **Character classes** supported: `[abc]`, `[a-z]`, `[!abc]`.\
-   **Parent un-ignore required**: e.g. `!bin/` then `!bin/keep/`.

Note: `.git/` itself is usually not in `.gitignore`. Exclude via
`ExcludeGlobs` instead.

------------------------------------------------------------------------

## Testing

-   Deterministic unit tests using `FakeFileSystem` and
    `FakeProcessRunner`.\
-   Coverage includes globbing, config mapping, traversal semantics,
    `.gitignore`, **content rendering (indentation/lines/padding)**, and
    history rendering/parsing.

Run:

``` bash
dotnet test
```

------------------------------------------------------------------------

## Contributing

Contributions are welcome. Please: - Keep public APIs documented (XML
comments). - Add tests for new behavior. - Follow the folder/namespace
conventions.

------------------------------------------------------------------------

## License

Licensed under the **Apache License 2.0**. See `LICENSE.txt` for
details.
