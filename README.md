# ChessRealms.Engine

ChessRealms.Engine is a .NET 10 library for standard chess positions and legal
play. It uses bitboards and magic attack tables for move generation. The public
`ChessGame` API supports FEN import/export, legal moves, promotion, history and
undo, and standard checkmate, stalemate and supported draw rules. The repository
also includes tests, a console game, a perft runner, benchmarks and a magic-number
search tool.

It does not provide an AI/search engine, UCI, SAN/PGN, Chess960 or tournament
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

The package intentionally exposes only the high-level types in the
`ChessRealms.Engine` namespace. Bitboards, encoded moves, magic tables, raw
positions and repository tools are implementation details and may change
without becoming package contracts.
