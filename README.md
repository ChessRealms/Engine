# Engine

Chess engine with bitboard representation. The core library has no external package
dependencies. All six projects target .NET 10.

### Build and regression tests

Install the stable [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)
version **10.0.401**, as pinned in `global.json`. `rollForward: disable` requires
this exact SDK; `allowPrerelease: false` excludes previews. CI reads the same file.
Install PowerShell 7 (`pwsh`) and Git for the solution structure check. See
[CONTRIBUTING.md](CONTRIBUTING.md) for repository layout and contribution rules.
Run from the repository root:

```sh
dotnet --version
pwsh -NoProfile -File scripts/Verify-SolutionStructure.ps1
dotnet restore ChessRealms.Engine.slnx --locked-mode
dotnet build ChessRealms.Engine.slnx --configuration Release --no-restore
dotnet test ChessRealms.Engine.slnx --configuration Release --no-build --filter "TestCategory!=Deep"
```

For the ordinary developer loop, run `dotnet test` from the repository root.
This builds and runs all ordinary tests, including fast perft; no settings file is required.
The solution lives at the repository root; all six projects remain in `src`.
Tests continue to use NUnit 3 through VSTest, explicitly selected in `global.json`.
The existing filters and NUnit `Explicit` behavior are unchanged.

Committed `packages.lock.json` files pin direct and transitive package versions
and content hashes. CI uses `--locked-mode` to reject dependency drift. When
intentionally updating packages, run
`dotnet restore ChessRealms.Engine.slnx --force-evaluate`, review the
lock-file changes, and repeat the checks above. When updating the SDK, update
`global.json` and this README together, then regenerate/review the lock files with
that SDK. Restore requires access to NuGet.org or a cache containing the locked packages.

- Ordinary tests cover the existing core/parsing checks, every square for all
  piece/color combinations, and basic public `ChessGame` behavior.
- `Perft` includes seven reference positions at depths 1–3 and assertions for
  special-move counts (castling, en passant and promotions). FEN and expected
  values come from [Chess Programming Wiki](https://www.chessprogramming.org/Perft_Results);
  the source link is also beside the data in `PerftTests.cs`.
- `Deep` contains only additional expensive perft depths: 4–6 for the initial
  position and position 3, and 4–5 for the other positions. These tests also
  carry NUnit `Explicit`, so a plain `dotnet test` does not run them. They are
  opt-in for cost, not failing tests being suppressed. Select them explicitly:

```sh
dotnet test ChessRealms.Engine.slnx --configuration Release --no-build --filter "TestCategory=Deep"
```

Run both test commands after the Release build, which compiles all tests. Tests,
the console perft runner and benchmarks use the same `PerftDriver` implementation.

GitHub Actions (`.github/workflows/ci.yml`) checks solution structure, then runs
locked restore, Release build and the same fast-test command on Windows and Linux
with SDK 10.0.401, on pushes and pull requests.
Deep tests are not part of the default CI job. A local pass does not establish
that either GitHub Actions job has passed.

Optional coverage check using the existing VSTest collector:

```sh
dotnet test ChessRealms.Engine.slnx -c Release --no-build --filter "TestCategory!=Deep" --collect:"XPlat Code Coverage"
```

### Tool smoke checks

After the Release build, run from the repository root:

```sh
dotnet run --project src/ChessRealms.Engine.Perft -c Release --no-build
dotnet run --project src/ChessRealms.MagicBruteforce -c Release --no-build --no-launch-profile -- 42 1
dotnet run --project src/ChessRealms.Engine.Benchmark -c Release --no-build -- --job Dry --inProcess --filter "*StartPos_Depth_6*" --wakeLock None
dotnet run --project src/ChessRealms.Engine.Console -c Release --no-build
```

Perft runs the initial position at depth 6: expect **119,060,324 nodes**.
MagicBruteforce tries one candidate per square/piece type; failure to find magic
numbers with this deliberately tiny budget is expected. In the interactive Console,
enter `e2e4`, then `e7e5`, check the board and side to move, then stop with Ctrl+C.
The benchmark command runs one Dry measurement to check startup and execution;
its result is not a performance comparison. Without arguments, the benchmark keeps
its original full `MediumRun` configuration; explicit arguments use BenchmarkDotNet's
CLI configuration instead.

See [the .NET 10 migration record](docs/dotnet-10-migration.md) for package
compatibility sources, baseline results and validation limits.

Implemented game rules and remaining support boundaries are in
[docs/known-issues.md](docs/known-issues.md).

### To Do:
- [X] Basic bitboard and square management operations.
- [X] Movegen for all moves.
- [X] Parsing FEN to `Position`.
- [X] Simple Perft (no hashtables or parallel calculations).
- [ ] Algebraic notation parsing.
- [ ] PGN (optional).
- [ ] Hashtables for Perft.
- [ ] UCI.
- [X] Standard game API, history, undo and draw rules.


### Historical perft benchmarks (.NET 8)

These measurements predate the .NET 10 migration and have not been remeasured.
The SDK below describes the historical benchmark environment, not the current build requirement.

Recorded perft benchmarks for _Initial Position_ `rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1` with _depth_ `6`.

- No hashtables.
- No parralel calculations. Everying is in 1 thread.
- `MakeMove`/`UndoMove` with copy `Position` (bitboards) approach.
- _Movegen_ gives pseudolegal moves then check for _IsKingChecked()_ determines if move is legal

See also about Perft: https://www.chessprogramming.org/Perft_Results

Benchmarks collected using `BenchmarkDotNet`.

#### Runned on
`Intel Core i5-8300H CPU 2.30GHz (Coffee Lake), 1 CPU, 8 logical and 4 physical cores`

`.NET SDK 8.0.205`

#### Results

| Method           | Mean    | Error    | StdDev   |
|----------------- |--------:|---------:|---------:|
| StartPos_Depth_6 | 3.564 s | 0.0484 s | 0.0429 s |

_This is average result._
_Sometimes benchmarks could be a bit faster or a bit slower._
_(`~3.443 s` or `~3.613 s`)_

### Game API

See [the complete API guide](docs/game-rules-api.md) for contracts, FEN validation,
FIDE draw rules, compatibility changes and exact dead-position detection limits.

```csharp
using ChessRealms.Engine;

var game = new ChessGame();
var moves = game.GetLegalMoves();            // read-only snapshot, initially 20
var copy = game.Clone();                    // independent history and repetitions
var result = copy.MakeMove(AlgebraicMove.Parse("e2e4"));
Console.WriteLine(copy.ToFen());            // ... b KQkq e3 0 1
Console.WriteLine(copy.Outcome);            // result, winner, reason
copy.UndoMove();
Console.WriteLine(copy.Position == game.Position); // True
```

Promotion is explicit: use `a7a8q`, `a7a8r`, `a7a8b`, or `a7a8n`.
A required promotion without a suffix is rejected without changing the game.

```csharp
if (ChessGame.TryCreateFromFen("7k/P7/8/8/8/8/8/7K w - - 0 1", out var promotion))
    promotion.MakeMove(AlgebraicMove.Parse("a7a8n"));

if (game.AvailableDrawClaims.HasFlag(DrawClaim.ThreefoldRepetition))
    game.ClaimDraw(DrawClaim.ThreefoldRepetition);
```

Threefold repetition and 50 moves require a claim; fivefold and 75 moves finish
automatically, with checkmate taking priority. Intended-move claims are supported.
The side to move always switches after a successful move, including checkmate;
display the winner from `Outcome.Winner`.

`ChessGame` is now a class: use `Clone()` for analysis copies. Failed FEN creation
returns null; FEN parsing is strict. Counters use built-in `BigInteger` and preserve
large decimal values exactly. FEN import starts fresh repetition history.
`GetBoardToSpan` still fills a 64-element board (a1 = 0; empty = `ChessPiece.Empty`).

Console commands: `moves`, `fen`, `undo`, `claim3 [move]`, `claim50 [move]`,
`quit`, or a coordinate move. Undo is available after game completion.
