# ChessRealms.Engine

ChessRealms.Engine is a .NET 10 library for standard chess positions and legal
play. It uses bitboards and magic attack tables for move generation. The public
`ChessGame` API supports FEN import/export, legal moves, promotion, history and
undo, canonical SAN formatting and parsing, mainline PGN import/export,
JSON game records, and standard
checkmate, stalemate and supported draw rules. The repository also includes
tests, a console game, a perft runner,
benchmarks and a magic-number search tool.

It does not provide an AI/search engine, UCI, annotated PGN, Chess960 or tournament
services such as clocks and draw agreements. Dead-position recognition is
deliberately incomplete; see the
[game API guide](https://github.com/ChessRealms/Engine/blob/main/docs/game-rules-api.md)
for the exact boundary.

## Build and test

Install the .NET SDK version pinned in `global.json` and run from the repository
root:

```sh
dotnet restore ChessRealms.Engine.slnx --locked-mode
dotnet build ChessRealms.Engine.slnx -c Release --no-restore
dotnet test ChessRealms.Engine.slnx -c Release --no-build --filter "TestCategory!=Deep"
dotnet pack src/ChessRealms.Engine/ChessRealms.Engine.csproj -c Release --no-build
```

The `Deep` test category contains slower perft cases and is opt-in. See the
[contribution guide](https://github.com/ChessRealms/Engine/blob/main/CONTRIBUTING.md)
for contributor checks.

See the
[benchmark guide](https://github.com/ChessRealms/Engine/blob/main/docs/performance.md)
for core, game API, SAN and PGN timing and allocation measurements.

## Public API

```csharp
using System;
using ChessRealms.Engine;

var game = new ChessGame();
var branch = game.Clone();
var result = branch.MakeMove(CoordinateMove.Parse("e2e4"));

if (result != MoveResult.None)
{
    Console.WriteLine(branch.ToFen());
    ChessPiece piece = branch.GetPiece(Square.Parse("e4"));
    branch.UndoMove();
}
```

`ChessGame` is mutable: use `Clone()` for an independent branch. Coordinate
promotions require a suffix such as `a7a8q`. The
[game API guide](https://github.com/ChessRealms/Engine/blob/main/docs/game-rules-api.md)
describes ownership, FEN validation, draw claims, repetition and supported rule
boundaries.

SAN is available at the text boundary, with the game supplying the position:

```csharp
var game = new ChessGame();
CoordinateMove move = game.ParseSan("Nf3"); // Read-only; the move is not played.
string notation = game.FormatSan(move);   // Format before making the move.
game.MakeMove(move);
game.MakeSanMove("d5");                  // Resolve and play text directly.
```

`TryParseSan(ReadOnlySpan<char>, out CoordinateMove)` and
`TryFormatSan(CoordinateMove, Span<char>, out int)` support buffer-based use.
See the [SAN guide](https://github.com/ChessRealms/Engine/blob/main/docs/san.md)
for canonical spelling, state guarantees, historical moves and performance costs.

Exchange a single game's tags and mainline in PGN:

```csharp
PgnGame document = PgnGame.FromGame(game, result: PgnResult.WhiteWin);
string pgn = document.ToPgn();
PgnGame imported = PgnGame.Parse(pgn);
ChessGame replay = imported.ToGame();
```

`PgnGame` is an immutable snapshot. Its declared result can represent resignation
or a draw agreement without changing the engine's board outcome. Import accepts
canonical SAN and rejects comments, annotations and variations with a structured
diagnostic. See the [PGN guide](https://github.com/ChessRealms/Engine/blob/main/docs/pgn.md)
for result rules, custom starting positions, diagnostics and costs.

Save and restore the current move line, including a successful draw claim:

```csharp
string json = GameRecord.Serialize(game.ExportRecord());
ChessGame restored = ChessGame.FromRecordJson(json);
```

JSON records include a schema version, initial FEN, coordinate moves and an
optional draw claim. `FromRecordJson` validates and restores the game in one replay. The
restored game preserves history, repetition counts and undo support.
Use `GameRecord.Deserialize` when an immutable record is needed instead of a game.

The package intentionally exposes only the high-level types in the
`ChessRealms.Engine` namespace. Bitboards, encoded moves, magic tables, raw
positions and repository tools are implementation details and may change
without becoming package contracts.
