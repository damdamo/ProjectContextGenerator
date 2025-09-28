# ProjectContextGenerator

Generate a clean, shareable tree of your repository in Markdown with precise filtering that honors both **glob patterns**,
**.gitignore semantics**, and optional **config profiles**. Perfect for
pasting into PRs, issues, or LLM prompts without manual cleanup.

---

## Highlights

-   **Filtering:** Include/Exclude **globs** + `.gitignore` (Root-only
    or Nested).
-   **Config profiles:** Define reusable presets in `.contextgen.json` and
    select them at runtime.
-   **Traversal vs rendering:** Directories are always traversed (unless
    excluded/ignored), ensuring that file-only includes still produce
    the minimal folder structure.
-   **Flexible modes:** Combine `DirectoriesOnly` with include patterns
    to list only the directory skeleton for specific file types
    (e.g. "all folders that contain JSON files").
-   **History mode:** Optionally append recent Git commit messages
    (titles or titles+body) after the tree for extra project context.
-   **Clean architecture:** Domain vs Infrastructure separation;
    testable components; fake filesystem for deterministic tests.
-   **CI-ready:** GitHub Actions workflow included
    (`.github/workflows/ci.yml`).

---

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

---

## Requirements

-   **.NET 8 SDK**
-   **Git** (in `PATH`) if using the `history` feature.

---

## Quickstart

``` bash
# Build
dotnet build

# Run tests
dotnet test

# Run the console sample (prints a Markdown tree)
dotnet run --project ProjectContextGenerator.Console
```

---

## Configuration

Tree generation is driven by a JSON config file, typically named
`.contextgen.json`.\
The console automatically picks it up from the working directory unless
overridden with `--config`.

Example:

```jsonc
{
  "version": 1,
  "root": ".",
  "maxDepth": 3,
  "exclude": ["bin/", "obj/", ".git/", "node_modules/"],
  "gitIgnore": "Nested",
  "history": {
    "last": 20,
    "maxBodyLines": 6,
    "detail": "TitlesOnly",
    "includeMerges": false
  },
  "profiles": {
    "fast": { "maxDepth": 1, "directoriesOnly": true },
    "full": {
      "maxDepth": -1,
      "collapseSingleChildDirectories": false,
      "history": { "detail": "TitleAndBody" }
    },
    "csharp": {
      "include": ["*.cs", "*.csproj"],
      "exclude": ["bin/", "obj/"]
    }
  }
}
```

### Profiles

Profiles are partial configs that override the root config when
selected. History settings can also be overridden per-profile.

```bash
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

---

## Usage

### Console

``` bash
# Default: uses ./contextgen.json if present, profile "full" if selected
dotnet run --project ProjectContextGenerator.Console --profile full

# Explicit config path
dotnet run --project ProjectContextGenerator.Console --config ./configs/custom.json

# Override root at runtime
dotnet run --project ProjectContextGenerator.Console --root ../other-repo
```

---

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

---

## History

When enabled, a block of recent Git commits is appended after the tree
output. This is useful to give additional context about the latest
changes in a repository.

### History options

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

```markdown
## Recent Changes (last 5)
- feat: add history support
  parses commits using git log
  renders Markdown
- fix: handle Windows line endings
```

---

## Filtering precedence

1.  **Include globs** must match (if provided).\
2.  **.gitignore** must *not* ignore the path.\
3.  **Exclude globs** must *not* exclude the path.

A path is included only if **all three** checks pass.

---

## .gitignore semantics

-   **Last matching rule wins**.\
-   **Directory-only patterns** (`pattern/`) match the directory **and
    its subtree**.\
-   **Anchored** patterns (`/pattern`) relative to repo root (RootOnly)
    or containing folder (Nested).\
-   **Character classes** supported: `[abc]`, `[a-z]`, `[!abc]`.\
-   **Parent un-ignore required**: e.g. `!bin/` then `!bin/keep/`.

Note: `.git/` itself is usually not in `.gitignore`. Exclude via
`ExcludeGlobs` instead.

---

## Testing

-   Deterministic unit tests using `FakeFileSystem` and
    `FakeProcessRunner`.\
-   Coverage includes globbing, config mapping, traversal semantics,
    `.gitignore`, and history rendering/parsing.

Run:

``` bash
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

Licensed under the **Apache License 2.0**. See `LICENSE.txt` for
details.