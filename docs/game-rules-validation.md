# Game-rules validation — 2026-09-10

> Repository organization note: the solution now lives at the repository root
> as `ChessRealms.Engine.slnx`.
> Commands and results below preserve the paths used at the time of validation.
> See [CONTRIBUTING.md](../CONTRIBUTING.md) for current commands.

Base: `origin/main` at `49fc3a5` (`.NET 10` migration), containing `d8eb1e3`
(restored tests and CI). Work branch: `feature/complete-game-rules`.
No applicable AGENTS.md was present in the repository or ancestor directories.
The working tree was clean before this work.

Local environment: Windows, .NET SDK **10.0.401**, Release configuration.

| Check | Result |
| --- | --- |
| `dotnet restore src/ChessRealms.Engine.sln --locked-mode` | Passed; no lock-file changes |
| `dotnet build src/ChessRealms.Engine.sln -c Release --no-restore` | Passed; all six projects, 0 warnings, 0 errors |
| `dotnet test src/ChessRealms.Engine.sln -c Release --no-build --filter "TestCategory!=Deep"` | 1010 passed, 0 failed, 0 skipped |
| `dotnet test src/ChessRealms.Engine.sln -c Release --no-build --filter "TestCategory=Deep"` | 16 passed, 0 failed, 0 skipped; 29 seconds |
| Console scripted smoke | Passed: legal moves, invalid draw claim, Fool's Mate, explicit Black winner, terminal rejection, undo, exact FEN |
| `git diff --check` | Passed |

The 851 pre-existing ordinary tests still run; 159 new cases cover the public API,
FEN validation and buffer safety. The original 16 deep cases and their reference
counts were not changed. Initial-position depth 6 remains **119,060,324** nodes.

Three seeded playouts run up to 160 plies each. At every position they apply every
offered legal move to an independent clone, verify king safety, disjoint piece
bitboards and matching occupancy, round-trip FEN, and undo to structural Position
equality and equal game/history/repetition state.

The restored CI configuration remains unchanged and includes the new ordinary
tests automatically. This local Windows run does not claim a new Linux or GitHub
Actions result; no push or merge was performed.

The branch is configured to use origin and
`refs/heads/feature/complete-game-rules` as its upstream, with pushRemote origin.
The remote feature branch does not exist until the user pushes it; Git may show
the preconfigured upstream as `gone` in the meantime. It does not track main.
