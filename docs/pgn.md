# Portable Game Notation

PGN is a text exchange format for games. `PgnGame` supports one standard-chess
game with tags, an initial position, the mainline and a declared result.
It uses the existing [SAN API](san.md) to resolve each move against its preceding
position. Board input and engine callers continue using `CoordinateMove`.

## Model and export

```csharp
using ChessRealms.Engine;

var game = new ChessGame();
game.MakeSanMove("e4");
game.MakeSanMove("e5");
game.MakeSanMove("Nf3");

var tags = new Dictionary<string, string>
{
    ["Event"] = "Example",
    ["White"] = "Alice",
    ["Black"] = "Bob"
};
PgnGame document = PgnGame.FromGame(game, tags, PgnResult.WhiteWin);
string text = document.ToPgn();
// ... headers, then: 1. e4 e5 2. Nf3 1-0

using var writer = new StreamWriter("game.pgn");
document.WriteTo(writer); // Does not close or dispose the writer.
```

`InitialFen`, `Moves`, `Tags` and `Result` are immutable snapshots. Source tags
are copied; later moves, undo and tag edits do not affect the document. Moves
contain coordinate values, without a cached SAN string or FEN pair per entry.
Only the current line is captured, including only moves played since the
initial FEN. Moves preceding a FEN import cannot be recovered.

Export uses canonical SAN, invariant move numbers and LF line endings. It
orders the seven roster tags first, then other tags by ordinal name; quote and
backslash characters in values are escaped. Missing roster values default to
`?`, with `????.??.??` for Date and the selected result for Result. Movetext is
wrapped at whitespace to 79 columns, except an individual longer move-number
token. A blank line separates headers and moves and follows the final marker.

Custom starting positions generate `SetUp "1"` and `FEN` automatically, using
the initial position rather than the final board. A line starting with Black
uses the FEN's fullmove number followed by three periods. Counters beyond
`Int64` are preserved. Redundant starting-position FEN/SetUp tags are removed.
Supplied setup tags must form a valid pair and match the game's initial FEN.

Tag names are case sensitive: 1–255 ASCII letters, digits or underscores, with
a letter or digit first. Values may contain at most 255 UTF-16 code units;
controls, line separators and unpaired surrogates are rejected. Printable
Unicode values are supported as an extension to traditional PGN character
encoding. Use UTF-8 when writing files that contain them. Nonstructural metadata,
including dates, player names and custom tags, is preserved without semantic
interpretation. Duplicate tag names are rejected.

## Result rules

`PgnResult` maps `Unknown` to `*`, `WhiteWin` to `1-0`, `BlackWin` to `0-1` and
`Draw` to `1/2-1/2`. Unknown covers both ongoing games and an unavailable result.

| Source of result | Behavior |
| --- | --- |
| `FromGame` without an explicit result or Result tag | Uses `ChessGame.Outcome`. |
| Supplied Result tag | Uses that result; an explicit argument must agree. |
| Unfinished engine position | Any declared result is accepted, including resignation, timeout or an agreed draw. |
| Finished engine game | The result must match its outcome, including a successful draw claim. |
| Import | The mandatory final marker must agree with the Result tag, if present, and any ending established by replay. |

`ToGame()` returns an independent replay with board, counters, history,
repetition and undo. It does not apply the declared result as a new engine
ending. For example, the exported `1-0` above produces an ongoing engine game
because the moves alone do not establish White's win. Applications displaying
a match should retain `PgnGame.Result` as well as the replayed board.

A claimed draw exports as `1/2-1/2`, but PGN does not preserve its claim reason or
unplayed intended move. Replaying such a mainline may therefore produce an
ongoing engine game. For exact persistence of claims, use `GameRecord` and its
versioned JSON format. Natural endings and automatic draws are reconstructed
from the mainline under the engine's [rule boundaries](game-rules-api.md).

## Import and diagnostics

```csharp
ReadOnlySpan<char> input = "1. e4 e5 2. Nf3 *";
if (PgnGame.TryParse(input, out PgnGame? document, out ChessGame? game, out PgnDiagnostic? error))
{
    // Both outputs are non-null here. The game was built during PGN validation.
    Console.WriteLine($"Declared result: {document.Result}; board: {game.ToFen()}");
}
else
{
    Console.WriteLine($"{error.Code} at {error.Line}:{error.Column}: {error.Message}");
}
```

For the throwing form, use `PgnGame.Parse(input, out ChessGame game)`, which
returns the `PgnGame` document. Both overloads replay the mainline once and
return that validated game with history, counters, repetition and undo. The
document owns an immutable snapshot: changing or undoing the returned game
does not change its moves, tags or declared result. As with `ToGame()`, the
declared result does not create a new engine ending.

Use `PgnGame.Parse(input)` or the three-argument `TryParse` when only the
document is needed. Calling `ToGame()` on it later creates an independent
game through another replay. The overloads returning both objects avoid that
second replay when importing for immediate game use.

`Parse` throws `PgnParseException`, a `FormatException` exposing the same
`Diagnostic`. All overloads use the same validation and diagnostics.
`TryParse` returns false with a null document and, for the overload returning a
game, a null game on any format, legality or unsupported-feature error. This
includes errors after legal moves or after the termination marker; neither
output exposes a partially imported game.
Success returns a null diagnostic. Neither operation takes or mutates an
existing game.

Diagnostic offsets count zero-based UTF-16 code units relative to the supplied
span. Line and column are one-based; CRLF counts as one newline, and a tab as
one column. `PlyIndex` is the next zero-based mainline index, or null for header
errors. Codes distinguish invalid tags, duplicate tags, setup/FEN errors,
result mismatches, wrong move numbers, illegal/noncanonical SAN, moves after
an engine ending, missing termination and unsupported input.

Import accepts optional headers, an initial BOM, ASCII space/tab/newline/vertical
tab separators, and optional move numbers. Supplied numbers must match the
position; import allows zero or more periods with whitespace between them.
Output normalizes this layout. Only canonical SAN is accepted: required check
and mate suffixes, uppercase `O` for castling, explicit promotion and minimal
legal disambiguation. Tags use only `\"` and `\\` escapes.

The following inputs are explicitly rejected:

- Comments (`{...}` or `;`), NAGs (`$1`), annotation suffixes (`!`, `?!`, etc.)
  and recursive variations (`(...)`), including after the final result.
- Escape lines (`%`), reserved angle-bracket constructs and extra content after
  the game. Multiple games must be separated by the caller using a suitable PGN
  database reader; this API does not provide one.
- Nonstandard variants. A supplied Variant tag must be exactly `Standard`.
- Missing result markers, malformed tags and unplayable mainlines.

No annotations or variations are silently discarded. This is a defined subset
of the [PGN specification](https://www.saremba.de/chessgml/standards/pgn/pgn-complete.htm),
not a lossless editor for arbitrary PGN databases. Original whitespace, tag order
and redundant setup tags are normalized on export.

## Costs and validation

PGN import consumes SAN from character spans, but processing a document still
allocates model and replay data. Use the `Parse`/`TryParse` overload returning
the game when both are needed; keep that instance for subsequent operations.
`ToGame()` performs another replay each time. `WriteTo` avoids creating the
complete output string. Coordinate-based move application pays no PGN processing
cost.

Export advances one position through the validated mainline, sharing legal-move
generation and SAN formatting with `ChessGame`. It does not build another game
history, undo stack or repetition dictionary. Move numbers use span formatting
with a string fallback for unusually large counters. Parsing the initial FEN,
ordering tags and storing output can still allocate.

Tests cover model ownership, result conflicts, custom FEN/counters, escaping,
canonical output, structured diagnostics, unsupported features, special moves,
automatic endings and deterministic game round trips through undo. The package
consumer smoke test exercises PGN through the built NuGet artifact.

See the [benchmark guide](performance.md) to measure import, export, snapshots
and restoration.
