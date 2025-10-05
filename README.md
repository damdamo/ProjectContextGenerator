# ProjectContextGenerator

A small tool that turns a repository into a clean Markdown context for LLM prompts:
- a filtered directory tree that respects `.gitignore`
- targeted file content excerpts selected by patterns and indentation
- optional recent Git history

Paste the output into your prompt so the model and you talk about the same project, with less back and forth.

---

## Motivation

Since large language models became part of my workflow, I start most tasks by giving them project context. That works until I need a fresh chat on the same project, or the current chat slows down and I restart, losing everything I curated. I wanted a quick way to rebuild context without copy pasting and pruning by hand.

ProjectContextGenerator collects the main pieces automatically so you can re-establish context in seconds, then add detail only where you need it.

### How the tool helps

1. **Repository structure** gives a tree folder structure of the files and directories.  
2. **Content excerpts** show only the parts that matter. Selection is driven by file patterns and **indentation**, a simple proxy for structure that works across most languages (by syntax or convention).  
3. **Recent changes** reuse the git commit messages, similarly to the `.gitignore` to remove irrelevant files out. Commit messages keep the conversation focused on what changed and why.

The exact files you surface will vary by language. For example: `README.md` and `.csproj` for C#, `package.json` for JS/TS, `pyproject.toml` for Python. Profiles help you start with sensible defaults.

### Additional use cases

It may: 
- **only keep function signatures from a file**
- **only keep Interface file**
- **work on one feature**: list the files that define it, with or without content. 
- **focus on current work**: enable history and include titles or titles plus body to anchor the discussion in what just happened.

---

## Quickstart

1) Add `.contextgen.json` at the repo root

```jsonc
{
  "version": 1,
  "root": ".",
  "maxDepth": 4,
  "include": [ "**/*" ],
  "exclude": [ "**/bin/**", "**/obj/**", ".git/" ],
  "gitIgnore": "Nested",
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
    "csharp-essential": { ... },
    "configs-and-docs": { ... },
    "history-title-only": { ... },
    "history-detailed": { ... },
    "test-oriented": { ... }
  }
}
```

There is a ready-to-use example in `ProjectContextGenerator.Console/.contextgen.json`.

2) Run from the solution root or the console project folder

```bash
dotnet run --project ProjectContextGenerator.Console --profile readme-focus
```

3) Paste the output into your prompt  
Add a note like: “Use only the context below. If something is missing, ask for a specific file or path.”

---

## Built-in profiles

| Profile               | What it shows                                                                 | When to use                                     |
|-----------------------|-------------------------------------------------------------------------------|-------------------------------------------------|
| `tree-only`           | Full tree, no content, no history                                             | Orientation and vocabulary of the repo          |
| `directories-only`    | Folders only                                                                  | Quick map when you just need structure          |
| `readme-focus`        | Tree plus full `README` content                                               | Project intro                                   |
| `csharp-essential`    | Tree plus `README` and `.csproj` content plus recent commit titles            | Onboarding and awareness of recent changes      |
| `configs-and-docs`    | Tree plus docs and configs (`*.md`, `*.json`, `*.yml`, `.gitignore`, etc.)    | Policy or config work                           |
| `history-title-only`  | Tree plus recent commit titles                                                | “What changed lately?”                          |
| `history-detailed`    | Tree plus commit titles and body                                              | Handoffs and change rationales                  |
| `test-oriented`       | Tree filtered on test folders plus `.cs` content in tests                     | Writing or fixing tests                         |

Use a profile:

```bash
dotnet run --project ProjectContextGenerator.Console --profile csharp-essential
```

---

## How matching works

You choose what appears in the tree and which files show content using familiar patterns. If you know `.gitignore`, this will feel natural.

**Pattern cheatsheet**

| Pattern        | Matches                                               |
|----------------|--------------------------------------------------------|
| `**/*.cs`      | every C# file                                         |
| `bin/`         | everything inside any `bin` folder                     |
| `README.md`    | any file named exactly `README.md`                     |
| `src/**/I*.cs` | C# files starting with `I` anywhere under `src/`       |

Two separate decisions:

1. What the tree shows using include and exclude, `.gitignore`, depth, and so on.  
2. Which **visible** files show content using `content.include`.

`content.include` does not bring excluded files back into the tree.

---

## Configuration

The tool reads `.contextgen.json` from the current directory.  
Precedence for root: `--root` argument, then `root` in config, then `"."`.

### Content options

Show the right parts of code and keep the conversation focused. Indentation tracks scope in most languages, so `indentDepth` gives you a simple dial for how deep to go.

| Key                       | Type (default)   | What it controls                                           | Why it helps in practice                                  | Validation and fallback            |
|---------------------------|------------------|------------------------------------------------------------|-----------------------------------------------------------|-----------------------------------|
| `content.enabled`         | bool (false)     | Turn content rendering on or off                           | Keep excerpts targeted instead of dumping everything       |                                   |
| `content.include`         | string[]         | Which visible files will show content                      | Limits attention to files you want to discuss             | Patterns normalized like the tree |
| `content.indentDepth`     | int (1)          | Keep lines with indent less than or equal to this level    | 0 for top-level, 1–2 for signatures and interfaces, −1 full | < −1 coerced to −1                |
| `content.contextPadding`  | int (1)          | Lines kept around retained lines                           | Keeps nearby lines so replies point to the right spot     | < 0 coerced to 0                  |
| `content.maxLinesPerFile` | int (300)        | Cap lines per file after filtering                         | Avoids very long excerpts that blur the discussion         | 0 or < −1 falls back to 300       |
| `content.showLineNumbers` | bool (false)     | Add line numbers                                           | Useful when changes are local, for example “edit lines 42–60” |                                   |
| `content.tabWidth`        | 2 or 4 or 8 (4)  | How many spaces a tab represents for analysis              | Leave detection on unless styles are mixed                | other values fall back to 4       |
| `content.detectTabWidth`  | bool (true)      | Auto-detect a common indentation width                     | Usually leave this on                                     |                                   |
| `content.maxFiles`        | int? (null)      | Max number of files that will render content               | Keep the initial selection small when starting fresh       | < 0 ignored (treated as null)     |

Recipes:
- top-level only: `indentDepth = 0`, `contextPadding = 0`  
- signatures or interfaces: `indentDepth = 1..2`, `content.include = ["**/I*.cs", "*.csproj", "README.md"]`  
- full files: `indentDepth = −1`, `maxLinesPerFile = −1` (use with care)

### History options

Use Git history to keep the discussion tied to what just happened.

| Key                     | Type (default)                   | What it controls                                  | Why it helps                                      | Validation and fallback     |
|-------------------------|----------------------------------|---------------------------------------------------|---------------------------------------------------|-----------------------------|
| `history.enabled`       | bool (true in config)            | Turn history on or off                            | Add only when it informs the task                 |                             |
| `history.last`          | int (10)                         | Number of recent commits to include               | Enough to follow the current thread of work       | < 0 coerced to 0           |
| `history.detail`        | "TitlesOnly" or "TitleAndBody"   | Whether to show body lines                        | Titles for quick scan, body when rationale matters | unknown falls back to TitlesOnly |
| `history.maxBodyLines`  | int (6)                          | Max body lines per commit                         | Keeps bodies short and readable                   | < 0 coerced to 0           |
| `history.includeMerges` | bool (false)                     | Include merge commits                             | Turn on only if merges carry useful context       |                             |

---

## Create your own profile

A profile is a named preset inside `profiles` in `.contextgen.json`. It only overrides what you put inside it. You run it with `--profile`.

### Step 1: Open or create `.contextgen.json`

Put this file at the root of your repo. If you already have one, keep your settings and add the profile below.

### Step 2: Add a profile (example: `csharp-essential`)

This one is aimed at C# projects. It keeps the full tree, renders `README.md` and `.csproj` with shallow content, and appends recent commit titles. If you need more options, check the **Content options** and **History options** tables above.

```jsonc
{
  "version": 1,
  "root": ".",
  "maxDepth": 4,
  "include": [ "**/*" ],
  "exclude": [ "**/bin/**", "**/obj/**", ".git/" ],
  "gitIgnore": "Nested",
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
    "csharp-essential": {
      // quick onboarding for C# repos
      "exclude": [ "**/bin/**", "**/obj/**", ".git/" ],
      "content": {
        "enabled": true,
        "include": [ "README.md", "*.csproj" ],
        "indentDepth": 1,
        "contextPadding": 1
      },
      "history": { "enabled": true } // uses last=10, TitlesOnly from root
    }

    // ...add more profiles here if you want
  }
}
```

### Step 3: Run it

```bash
dotnet run --project ProjectContextGenerator.Console --profile csharp-essential
```

You will get a clean tree, focused content for `README.md` and `.csproj`, and a “Recent Changes” block with commit titles.

### Handy variations

You can tweak `csharp-essential` without touching the rest of the file. Copy one of these into the profile. For the full list of switches, see the option tables above.

**Show commit details too (titles and body)**

```jsonc
"profiles": {
  "csharp-essential": {
    "history": {
      "enabled": true,
      "detail": "TitleAndBody",
      "maxBodyLines": 6
    }
  }
}
```

**Add another content target, for example interfaces**

```jsonc
"profiles": {
  "csharp-essential": {
    "content": {
      "enabled": true,
      "include": [ "README.md", "*.csproj", "**/I*.cs" ],
      "indentDepth": 1,
      "contextPadding": 1
    }
  }
}
```

**Show line numbers in content**

```jsonc
"profiles": {
  "csharp-essential": {
    "content": {
      "showLineNumbers": true
    }
  }
}
```

**Tip**  
If you only want signatures or other surface-level code, keep `indentDepth` at 0 or 1.  
If you truly need full files, set `indentDepth` to −1.

---

## CLI

```bash
# Use default config from the working directory
dotnet run --project ProjectContextGenerator.Console

# Pick a profile
dotnet run --project ProjectContextGenerator.Console --profile history-detailed

# Use a specific config file
dotnet run --project ProjectContextGenerator.Console --config ./configs/context.small.json

# Override root at runtime
dotnet run --project ProjectContextGenerator.Console --root ../other-repo
```

### Workflow tip: keep a single generic config

An easy setup is to maintain one generic `.contextgen.json` in a shared folder and always run with `--config`:

```bash
# From any repo you want to summarize
dotnet run --project ProjectContextGenerator.Console \
  --config ~/context-presets/csharp-essential.json
```

You can keep several presets in that folder, for example `readme-focus.json`, `history-detailed.json`, or language-specific variants. This avoids duplicating config files across repositories.

Quick reference:

| Flag         | When to use                               |
|--------------|-------------------------------------------|
| `--profile`  | pick a preset in the current config       |
| `--config`   | point to a specific config file           |
| `--root`     | generate context for another folder       |

---

## Prompt-ready example

```markdown
# Repository Context

## Tree
/your-repo/
- README.md
- src/
  - YourProject.csproj
  - Api/
  - Domain/
- tests/

## Content
- README.md

  # Your Project
  Short description...
  
- src/YourProject.csproj
  <Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
      <TargetFramework>net8.0</TargetFramework>
    </PropertyGroup>
  </Project>

## Recent Changes (last 10)
- feat: add project template
- fix: restore package lock
- chore: bump dependencies
…
```

You can add a note under your prompt such as: “Use only the context above. If more is needed, ask for a specific file or path. When suggesting edits, reference line numbers if present.”

---

## Requirements

- .NET 8 SDK  
- Git on PATH if you use history

---

## Project layout

```
/ProjectContextGenerator
├─ ProjectContextGenerator.Console/
├─ ProjectContextGenerator.Domain/
│  ├─ Abstractions  ├─ Config  ├─ Models
│  ├─ Options       ├─ Rendering  ├─ Services
├─ ProjectContextGenerator.Infrastructure/
│  ├─ FileSystem  ├─ Filtering  ├─ GitIgnore  ├─ Globbing
├─ ProjectContextGenerator.Tests/
│  ├─ ConfigTests  ├─ Fakes  ├─ GlobbingTests
│  ├─ RenderingTests  ├─ TreeBuilderTests
└─ ProjectContextGenerator.sln
```

---

## Contributing

- Document public APIs with XML comments  
- Add tests for new behavior  
- Keep the folder and namespace conventions

---

## License

Apache License 2.0 (`LICENSE.txt`).
