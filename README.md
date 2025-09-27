# ProjectContextGenerator

Generate a clean, shareable tree of your repository (Markdown, plain
text, JSON) with precise filtering that honors both **glob patterns**,
**.gitignore semantics**, and optional **config profiles**. Perfect for
pasting into PRs, issues, or LLM prompts without manual cleanup.

---

## Highlights

-   **Multiple renderers:** Markdown / Plain text.
-   **Filtering:** Include/Exclude **globs** + `.gitignore` (Root-only
    or Nested).
-   **Config profiles:** Define reusable presets in `.treegen.json` and
    select them at runtime.
-   **Traversal vs rendering:** Directories are always traversed (unless
    excluded/ignored), ensuring that file-only includes still produce
    the minimal folder structure.
-   **Flexible modes:** Combine `DirectoriesOnly` with include patterns
    to list only the directory skeleton for specific file types
    (e.g. "all folders that contain JSON files").
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
`.treegen.json`.\
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
  "profiles": {
    "fast": { "maxDepth": 1, "directoriesOnly": true },
    "full": { "maxDepth": -1, "collapseSingleChildDirectories": false },
    "csharp": { "include": ["*.cs", "*.csproj"], "exclude": ["bin/", "obj/"] }
  }
}
```

### Profiles

Profiles are partial configs that override the root config when
selected:

``` bash
dotnet run --project ProjectContextGenerator.Console --profile fast
dotnet run --project ProjectContextGenerator.Console --profile csharp
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
# Default: uses ./treegen.json if present, profile "full" if selected
dotnet run --project ProjectContextGenerator.Console --profile full

# Explicit config path
dotnet run --project ProjectContextGenerator.Console --config ./configs/custom.json

# Override root at runtime
dotnet run --project ProjectContextGenerator.Console --root ../other-repo
```

### Options overview

-   `MaxDepth`: `0` = root only; `1` = root + direct children; `-1` =
    unlimited.
-   `IncludeGlobs` / `ExcludeGlobs`: optional glob lists (e.g.,
    `["**/*.cs"]`, `["**/bin/**"]`).
-   `SortDirectoriesFirst`: default `true`.
-   `CollapseSingleChildDirectories`: default `true` (render `a/b/c/` as
    `a/b/c/`).
-   `MaxItemsPerDirectory`: optional cap with placeholder node
    (`… (+N more)`).
-   `GitIgnore`: `None` \| `RootOnly` \| `Nested`.
-   `GitIgnoreFileName`: typically `".gitignore"`.
-   `DirectoriesOnly`: default `false`.

### Combinations

-   **Files only**:

    ``` json
    { "include": ["*.json"] }
    ```

    → shows only `.json` files and their parent folders.

-   **DirectoriesOnly + file includes**:

    ``` json
    { "directoriesOnly": true, "include": ["*.json"] }
    ```

    → shows only the folder skeleton containing at least one `.json`
    file.

-   **Exclude wins over include**:\
    Even if `include: ["*.cs"]`, any path under `exclude: ["bin/"]` is
    pruned.

-   **CollapseSingleChildDirectories**:\
    Useful for deep nested chains:\
    `src/utils/helpers/core/ → File.cs`

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
-   **Parent un-ignore required**: e.g. `!bin/` then `!bin/keep/`.

Note: `.git/` itself is usually not in `.gitignore`. Exclude via
`ExcludeGlobs` instead.

---

## Renderers

-   **Markdown**: human-friendly tree (folders end with `/`).\
-   **PlainText**: simple ASCII output.\
-   **JSON**: raw machine-readable structure.

---

## Testing

-   Deterministic unit tests using `FakeFileSystem`.\
-   Coverage includes globbing, config mapping, traversal semantics,
    renderer stability, and `.gitignore`.

Run:

``` bash
dotnet test
```

---

## Contributing

Contributions are welcome. Please: - Keep public APIs documented (XML
comments). - Add tests for new behavior. - Follow the folder/namespace
conventions.

---

## License

Licensed under the **Apache License 2.0**. See `LICENSE.txt` for
details.
