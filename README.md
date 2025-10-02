# ProjectContextGenerator

Create a clean, **shareable context** of your repository in Markdown:
- A **filtered directory tree** (respects your `.gitignore`).
- Optional **file content excerpts** (top-level only, or deeper by indentation).
- Optional **recent Git history** (titles or titles + body).

It’s perfect for pasting into PRs, issues, or LLM prompts—no manual cleanup.

---

## Why this tool?

- **Zero friction**: one file (`.contextgen.json`) + one command.
- **Readable by default**: directory-first, depth caps, content trimmed by indentation.
- **Profiles**: pick a preset for the view you need (tree-only, docs, C# essentials, history…).

---

## 1-Minute Quickstart

1) **Add a config** at your repo root: `.contextgen.json`

```jsonc
{
  "version": 1,
  "root": ".",
  "maxDepth": 4,
  "include": [ "**/*" ],
  "exclude": [ "**/bin/**", "**/obj/**", ".git/" ],
  "gitIgnore": "RootOnly",
  "sortDirectoriesFirst": true,
  "collapseSingleChildDirectories": true,

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
    "enabled": false,
    "last": 10,
    "maxBodyLines": 6,
    "detail": "TitlesOnly",
    "includeMerges": false
  },

  "profiles": {
    "tree-only": { ... },
    "directories-only": { ... },
    "readme-focus": { ... },
    "csharp-essential": { ... },      // README + *.csproj content + history enabled
    "configs-and-docs": { ... },
    "history-title-only": { ... },
    "history-detailed": { ... },
    "test-oriented": { ... }
  }
}
```

> A ready-to-use example file is available in the repo at  
> **`ProjectContextGenerator.Console/.contextgen.json`**.

2) **Run** (from the solution root or the console project folder):

```bash
dotnet run --project ProjectContextGenerator.Console --profile readme-focus
```

You’ll get Markdown like:

```markdown
/your-repo/
- README.md
  ```
  # Your Project
  Short description...
  ```
```

---

## Built-in Profiles (batteries included)

These presets cover most day-to-day needs. Use them as-is or as a base for your own.

| Profile               | What you get                                                                 |
|-----------------------|------------------------------------------------------------------------------|
| `tree-only`           | Full tree (files + folders). No content. No history.                        |
| `directories-only`    | Folders only (skeleton). No content. No history.                            |
| `readme-focus`        | Tree + **README** content (full). No history.                               |
| `csharp-essential`    | Tree + **README** and **`.csproj`** content **+ recent commits (titles)**.  |
| `configs-and-docs`    | Tree + **docs/configs** content (`*.md`, `*.json`, `*.yml`, `.gitignore`, etc.). |
| `history-title-only`  | Tree + **recent commits (titles)**. No content.                             |
| `history-detailed`    | Tree + **recent commits (titles + body)**. No content.                      |
| `test-oriented`       | Tree filtered on test folders + **`.cs` content** in tests.                 |

Use a profile:

```bash
dotnet run --project ProjectContextGenerator.Console --profile csharp-essential
```

---

## How matching works (plain English)

You select what appears and what shows content using **file patterns**.  
If you’ve ever used **`.gitignore`**, you already know the idea:

- `**/*.cs` → every C# file (any folder).  
- `bin/` → everything inside any `bin` folder.  
- `README.md` → any file named exactly `README.md`.  
- `src/**/I*.cs` → C# files starting with `I` anywhere under `src/`.

> Under the hood, the tool uses **`.gitignore`-style logic**: intuitive includes/excludes and `folder/**` for subtrees. You don’t need to be a glob expert—copy the examples and adapt them.

### Two separate decisions (by design)

1. **What the tree shows** (folders/files)  
   Controlled by *tree options* (`include` / `exclude`, `.gitignore`, depth, etc.).

2. **Which files show their content**  
   Controlled by **`content.include`** — **a list of patterns**.  
   If a visible file matches one of these, its content is rendered.

> ✅ Content selection does **not** “revive” files removed from the tree.  
> If a file isn’t in the tree, it won’t show content (even if `content.include` matches).

---

## Configuration (practical)

The tool looks for `.contextgen.json` at the current directory.  
CLI precedence: `--root` > config `root` > `"."`.

### Content options (most useful first)

- `content.enabled` (`bool`) — turn content on/off.  
- **`content.include` (`string[]`)** — **the list that drives content**. Only files matching these patterns will show their content.  
- `content.indentDepth` (`int`, default `1`) — keep lines with indent ≤ depth. `-1` = keep all.  
- `content.contextPadding` (`int`, default `1`) — extra lines around kept lines.  
- `content.maxLinesPerFile` (`int`, default `300`; `-1` = unlimited).  
- `content.showLineNumbers` (`bool`, default `false`).  
- `content.tabWidth` (`2/4/8`, default `4`) and `content.detectTabWidth` (`bool`, default `true`).  
- `content.maxFiles` (`int?`, default `null`) — global cap for how many files render content.

### History options

- `history.enabled` (`bool`, default `true`)  
- `history.last` (`int`, default `10`)  
- `history.detail` (`"TitlesOnly"` or `"TitleAndBody"`, default `TitlesOnly`)  
- `history.maxBodyLines` (`int`, default `6`)  
- `history.includeMerges` (`bool`, default `false`)

---

## CLI usage

```bash
# Use default config from working dir
dotnet run --project ProjectContextGenerator.Console

# Pick a profile
dotnet run --project ProjectContextGenerator.Console --profile history-detailed

# Use a specific config file
dotnet run --project ProjectContextGenerator.Console --config ./configs/context.small.json

# Override root at runtime
dotnet run --project ProjectContextGenerator.Console --root ../other-repo
```

---

## Examples (what the output looks like)

### `directories-only`
```markdown
/your-repo/
- src/
  - Api/
  - Domain/
  - Infrastructure/
- tests/
- .github/
```

### `csharp-essential` (tree + README + .csproj + history titles)
```markdown
/your-repo/
- README.md
  ```
  # Your Project
  …
  ```
- src/
  - YourProject.csproj
    ```
    <Project Sdk="Microsoft.NET.Sdk">
      <PropertyGroup>
        <TargetFramework>net8.0</TargetFramework>
      </PropertyGroup>
    </Project>
    ```

## Recent Changes (last 10)
- feat: add project template
- fix: restore package lock
- chore: bump dependencies
…
```

### `history-title-only`
```markdown
/your-repo/
- src/
- tests/

## Recent Changes (last 20)
- feat(api): add OpenAPI endpoints
- fix(domain): null-check in mapper
- docs(readme): refresh quickstart
…
```

---

## Requirements

- **.NET 8 SDK**  
- **Git** (on `PATH`) if you use the history block

---

## Project layout (high level)

```
/ProjectContextGenerator
├─ ProjectContextGenerator.Console/
├─ ProjectContextGenerator.Domain/
│  ├─ Abstractions/  ├─ Config/  ├─ Models/
│  ├─ Options/       ├─ Rendering/ ├─ Services/
├─ ProjectContextGenerator.Infrastructure/
│  ├─ FileSystem/  ├─ Filtering/  ├─ GitIgnore/  ├─ Globbing/
├─ ProjectContextGenerator.Tests/
│  ├─ ConfigTests/  ├─ Fakes/  ├─ GlobbingTests/
│  ├─ RenderingTests/  ├─ TreeBuilderTests/
└─ ProjectContextGenerator.sln
```

---

## Tips

- Start with **`readme-focus`** for a clean, low-noise intro.  
- For C# repos, **`csharp-essential`** surfaces README + project files + history titles.  
- Need just the structure? **`tree-only`** or **`directories-only`**.  
- Want recent activity? **`history-title-only`** or **`history-detailed`**.

---

## Testing

```bash
dotnet test
```

Unit tests are deterministic via in-memory fakes and cover:
config mapping (including `content.include` normalization), matching,
tree traversal, content rendering (indentation/ellipsis/truncation), and history.

---

## Contributing

- Document public APIs (XML docs).  
- Add tests for new behavior.  
- Keep the folder/namespace conventions.

---

## License

Licensed under the **Apache License 2.0** (`LICENSE.txt`).
