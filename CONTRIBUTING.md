# Contributing

## Prerequisites and checks

Install Git, PowerShell 7 (`pwsh`), and .NET SDK **10.0.401**, the exact stable
version pinned in `global.json`. Restore needs access to NuGet.org or a cache
containing the locked packages. No additional PowerShell modules are required.

Run these commands from the repository root:

```sh
pwsh -NoProfile -File scripts/Verify-SolutionStructure.ps1
dotnet sln ChessRealms.Engine.slnx list
dotnet restore ChessRealms.Engine.slnx --locked-mode
dotnet build ChessRealms.Engine.slnx -c Release --no-restore
dotnet test ChessRealms.Engine.slnx -c Release --no-build --filter "TestCategory!=Deep"
git diff --check
```

For the ordinary developer loop, run `dotnet test` from the root. Run the structure
check whenever docs, scripts, the solution, or project membership/locations change.
For code, project, solution, SDK, dependency, or build/CI changes, also run locked
restore, the Release build, and the fast tests above. Documentation-only changes
need the structure check, link/command review, and `git diff --check`.

Deep tests and performance runs are opt-in when relevant to the change; see
[README.md](README.md) for commands. CI checks structure before locked restore,
Release build, and fast tests on both Windows and Linux. Report local results as
local results; do not infer a CI, Linux, or Visual Studio pass from them.

## Repository and solution structure

- `ChessRealms.Engine.slnx`: the root solution in XML `.slnx` format.
- `src/`: six projects (engine, tests, console, perft runner, benchmarks, and
  magic-number search). Each project stays in its own physical directory.
- `docs/`: API documentation, support boundaries, and historical validation records.
- `scripts/`: repository tooling, initially only `Verify-SolutionStructure.ps1`.
- `.github/workflows/ci.yml`: the Windows/Linux verification workflow.
- Root files such as `README.md`, `CONTRIBUTING.md`, `AGENTS.md`, `global.json`,
  and `Directory.Build.props`: repository instructions and shared configuration.

Keep solution folders synchronized whenever files or projects are added, removed,
renamed, or moved:

- `docs` and `scripts` mirror their physical subdirectories recursively. Add each
  tracked or new nonignored file once as a Solution Item in the matching folder,
  using its full path relative to the root solution in a `<File Path="..." />` entry.
  For example, `<File Path="docs/guides/setup.md" />` belongs in
  `<Folder Name="/docs/guides/">`. Do not add empty folders
  or ignored artifacts. Tracked files remain checked even if an ignore rule matches.
- Put all `.csproj` files under the matching `src` solution structure. A project
  node represents its own directory, so `src/Example/Example.csproj` belongs directly
  in `src`, and `src/tools/Example/Example.csproj` belongs in `src/tools`. A project
  directly in `src` also belongs in `src`. Do not duplicate project source files as
  Solution Items. Preserve existing project/folder `Id` values, project types,
  and build configurations. The standard Debug/Release and Any CPU mappings use
  SLNX defaults, verified against the previous solution during migration.
- Match spelling and case exactly, including on Windows. Both slash styles in
  solution paths are supported. SLNX folder elements are direct children of
  `<Solution>`; their absolute names, such as `/docs/guides/`, define nesting.
  Ancestor folders may be implicit. Record case-only renames in Git as well.

Keep a single root solution so `dotnet test` can discover it without ambiguity.
The SDK's `dotnet sln <file.sln> migrate` command converts a classic solution to
SLNX, but leaves the old file in place. After validating the migration, remove
the old solution and update current commands. See the
[official CLI documentation](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-sln).

Run the structure checker after editing the solution. It uses its own location
to find the repository, works from any current directory, reports discrepancies,
and exits nonzero on failure. It never fixes files automatically. Keep historical
reports accurate: preserve commands and results as originally run and add a note
pointing to current instructions when paths change. Root service files stay at
the root and do not need entries in `docs`.

## Namespaces and project names

`ChessRealms` is the organization prefix, matching the configured GitHub origin
`ChessRealms/Engine`. `ChessRealms.Engine` identifies the library;
suffixes such as `.Tests`, `.Console`, `.Perft`, and `.Benchmark` identify related
projects by purpose. `ChessRealms.MagicBruteforce` is a supporting tool under the
same organization. This follows the
[.NET namespace naming guidance](https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/names-of-namespaces).

Keep these names consistent across namespaces, project/assembly names, project
references, and `InternalsVisibleTo` declarations. Removing the organization prefix
would change public type names and, if projects are renamed, assembly identities.
Treat such a rename as an intentional compatibility change with a migration plan.
There is no technical requirement to rename them when changing solution format.

## Branches, commits, and pull requests

Use `<type>/<short-kebab-case-description>` for branches. Use
`<type>(<scope>): <short imperative description>` for commit and PR titles; choose
a concise scope such as `repo`, `engine`, `tests`, or `ci`.

| Branch type | Purpose | Commit / PR prefix |
| --- | --- | --- |
| `feature` | Add user-facing functionality | `feat` |
| `fix` | Correct a defect | `fix` |
| `docs` | Update documentation | `docs` |
| `test` | Add or improve tests | `test` |
| `refactor` | Restructure code without changing behavior | `refactor` |
| `perf` | Improve performance | `perf` |
| `ci` | Change continuous integration | `ci` |
| `chore` | Maintain tooling or repository organization | `chore` |

The branch type is **`feature`**, while the corresponding commit and PR type is
**`feat`**. All other branch types use the same prefix in commit and PR titles.
For example:

- `feature/add-uci-support` → `feat(engine): add UCI support`
- `fix/validate-promotion` → `fix(engine): validate promotion choices`
- `docs/update-api-guide` → `docs(api): clarify draw claims`
- `chore/repository-organization` →
  `chore(repo): organize solution, documentation and repository checks`

Describe the problem, resulting behavior, and scope in the PR. Include relevant
design choices or compatibility implications, the exact checks run and their
results (including test counts where available), and any checks not run or other
validation limits. Link a related issue when one exists. Keep the title and
description aligned with the final changes reviewers will see.

## Text files

Use UTF-8, LF line endings, and a final newline, following `.editorconfig` and
`.gitattributes`. Preserve existing indentation unless a change requires otherwise.
Git automatically classifies text files; do not convert binary files. Add CRLF
exceptions only when a tool demonstrably requires them. `git add --renormalize`
normalizes the index but does not rewrite every working-copy file. If normalizing
existing files, rewrite text files safely and verify that their content is unchanged
apart from line endings; avoid destructive checkout/reset operations.
