# Standard Algebraic Notation

SAN describes a move relative to the position **before** that move. It is useful
for a human-readable move list, text entry and as a building block for PGN.
The SAN methods handle one canonical move at a time. [PgnGame](pgn.md) builds on
them to import and export a single game's tags and mainline.

`CoordinateMove` remains the move value used by board input, history and game
records. A drag from e2 to e4 already supplies a coordinate move and needs no SAN
conversion. SAN is resolved at the text boundary. There is no separate public
`SanMove`: the text needs a position to identify its source square, while the
resolved value is already represented by `CoordinateMove`.

## Reading, displaying and playing moves

```csharp
using ChessRealms.Engine;

var game = new ChessGame();
CoordinateMove move = game.ParseSan("Nf3"); // Does not play the move.
string label = game.FormatSan(move);      // "Nf3", before applying it.
MoveResult result = game.MakeMove(move);

// For text entry that should immediately play a move:
MoveResult reply = game.MakeSanMove("d5");
```

The explicit parse-then-apply form is useful for previews and validation. For
immediate application, `MakeSanMove` reuses the resolved internal move instead
of searching the current position's legal moves again. Both application paths
have identical history, repetition, draw, outcome and undo behavior.

For caller-owned buffers:

```csharp
var game = new ChessGame();
ReadOnlySpan<char> input = "Nf3";
Span<char> output = stackalloc char[7];

if (game.TryParseSan(input, out CoordinateMove move)
    && game.TryFormatSan(move, output, out int written))
{
    ReadOnlySpan<char> notation = output[..written];
    // Consume notation here; allocating a string is optional.
}
```

Seven characters suffice for any canonical standard-chess SAN move, including
`Qa1xc3+` and `axb8=Q+`. The span API uses characters; there are no SAN UTF-8
overloads. SAN output is ASCII and culture independent.

## Canonical notation and strict parsing

The notation follows [PGN specification section 8.2.3](https://www.saremba.de/chessgml/standards/pgn/pgn-complete.htm#c8.2.3):

- Piece letters are `N`, `B`, `R`, `Q`, `K`; pawn moves omit a piece letter.
- Captures use `x`. Pawn captures include the source file, such as `exd5`.
- Castling is `O-O` or `O-O-O`, using uppercase letters O.
- En passant uses ordinary pawn-capture notation, such as `exd6`.
- Promotion uses `=N`, `=B`, `=R` or `=Q`, including underpromotion.
- Source disambiguation considers only other **legal** moves of the same piece
  type to the same target. Use the source file if sufficient, otherwise the
  rank, otherwise both. An absolutely pinned alternative does not qualify.
- A checking move ends in `+`, or `#` for mate. Discovered and double checks
  also use `+`; a checking suffix cannot replace source disambiguation.
- Stalemate and other drawn endings have no special SAN suffix. A move giving
  check while triggering an automatic draw still uses `+` when legal replies
  exist on the resulting board.

Parsing accepts exactly the canonical spelling produced for that move in that
position. Missing, false or incorrect check/mate suffixes and unnecessary source
qualifiers are rejected. For example, from the starting position `Nf3` is
accepted; `Ngf3`, `Ng1f3`, `nf3`, `Nf3+` and `Nf3!` are rejected.

There is no whitespace trimming, `0-0` alias, omitted capture marker, implicit
promotion, `e.p.` suffix, figurine notation, move number, result token, comment
or annotation support. PGN import handles move numbers and result markers
separately and retains the same canonical SAN requirement.

## Failure and state guarantees

| Operation | Failure behavior |
| --- | --- |
| `FormatSan(move)` | Throws `ArgumentException` for an invalid/unplayable move or a finished game. |
| `TryFormatSan(move, destination, out written)` | Returns false, writes zero characters and leaves the entire destination unchanged for an unplayable move, a finished game or insufficient space. |
| `ParseSan(text)` | Throws `FormatException` for invalid, ambiguous, noncanonical or unplayable SAN, including a finished game. |
| `TryParseSan(text, out move)` | Returns false with `default(CoordinateMove)` for the same inputs. |
| `MakeSanMove(text)` | Returns `MoveResult.None` without changing the game for the same inputs. |

An empty/default character span, including a null string converted to a span,
is invalid input. Parsing and formatting never change the board, counters,
history, repetition counts, outcome, draw claim or undo stack. They analyze a
value copy of the position. No move is temporarily applied to the original game.

All operations use the current game's playable-move policy: a finished game
rejects even a move that would otherwise be legal on the board. This includes
claimed and automatic draws. A parsed coordinate move is not a reservation;
`MakeMove` validates it again if applied later. Games remain mutable and are not
thread-safe.

## Formatting history

The current position cannot generally format a move that has already been
played. For a single entry, its `FenBefore` supplies the necessary board context:

```csharp
MoveHistoryEntry entry = game.History[0];
string san = ChessGame.FromFen(entry.FenBefore).FormatSan(entry.Move);
```

For a whole line, replay once from the record's initial position and format
each move before applying it:

```csharp
GameRecord record = game.ExportRecord();
var replay = ChessGame.FromFen(record.InitialFen);
var labels = new List<string>(record.Moves.Count);
foreach (CoordinateMove move in record.Moves)
{
    labels.Add(replay.FormatSan(move));
    replay.MakeMove(move);
}
```

A draw claim's intended move was not played and is not in `record.Moves`. A
game loaded from FEN only has the history played since that FEN. Formatting
historical moves does not recover earlier moves or encode a game's result.

## Performance and validation

After initialization, successful `TryParseSan`, `ParseSan` and `TryFormatSan`
queries require no managed heap allocations. `FormatSan` allocates the returned
string; throwing APIs allocate exceptions on failure. SAN still requires legal
move generation and check/mate detection.

`MakeSanMove` reuses the resolved internal move for application. Applying it
still has the normal game history and repetition costs. Coordinate-based callers
pay no SAN formatting or parsing cost.

`SanTests` covers canonical fixtures, strict rejection, state preservation,
undo, deterministic playout round trips and allocation checks after warmup.
`SanBenchmarks` compares formatting, parsing and application paths; see the
[benchmark guide](performance.md) for commands and measurement guidance.
