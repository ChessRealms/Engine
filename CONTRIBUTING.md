# Contributing

Use the .NET SDK pinned in `global.json`, PowerShell 7 (`pwsh`) and Git. Run
commands from the repository root. Keep source, project and solution paths in
sync; `scripts/Verify-SolutionStructure.ps1` checks the expected structure.

## Validation

For code, project, solution, SDK, dependency or CI changes, run:

```sh
pwsh -NoProfile -File scripts/Verify-SolutionStructure.ps1
dotnet restore ChessRealms.Engine.slnx --locked-mode
dotnet build ChessRealms.Engine.slnx -c Release --no-restore
dotnet test ChessRealms.Engine.slnx -c Release --no-build --filter "TestCategory!=Deep"
git diff --check
```

For documentation-only changes, run the solution-structure check, review links
and commands, and run `git diff --check`. Run the structure check whenever docs,
scripts, the solution or project membership/locations change. Deep perft tests
are optional when relevant:

```sh
dotnet test ChessRealms.Engine.slnx -c Release --no-build --filter "TestCategory=Deep"
```

Commit and review any intentional package lock-file changes after dependency
updates. Report the exact checks run and any validation limits in pull requests.

## Change conventions

Use UTF-8, LF line endings and a final newline, as configured by `.editorconfig`
and `.gitattributes`. Preserve public API compatibility unless a change
explicitly calls for a breaking release.

Use `<type>/<short-description>` branch names and
`<type>(<scope>): <imperative summary>` commit and PR titles. Common types are
`feature` (title type `feat`), `fix`, `docs`, `test`, `refactor`, `perf`, `ci`
and `chore`. In a PR, explain the behavior changed, relevant design or
compatibility implications, and validation performed.
