# Game API and rule boundaries

`ChessGame` is a mutable, sealed standard-chess game. A new game starts at the
usual position; `FromFen` and `TryCreateFromFen` start from a supplied position.
The game generates legal moves, applies them, records successful moves and
classifies endings. Move generation uses bitboards and magic attack tables;
pseudolegal moves are filtered for king safety.

## Moves, state and ownership

- `CoordinateMove.Parse` accepts lowercase coordinate moves such as `e2e4` and
  promotion moves ending in `q`, `r`, `b` or `n`. Parsing checks syntax; the game
  checks legality. Promotion has no default queen: `a7a8` is rejected if a
  promotion is required. `Parse` throws on malformed input; `TryParse` returns
  false.
- `Square` and `CoordinateMove` also implement `IUtf8SpanParsable<T>` and
  `IUtf8SpanFormattable`. For example, `CoordinateMove.Parse("e2e4"u8)` reads
  UTF-8 notation and `move.TryFormat(buffer, out int written)` writes it into a
  byte span. Formatting accepts an empty format or `G`/`g`; other formats throw
  `FormatException`. Parsing and formatting are culture independent and ignore
  the provider. An invalid default value or a short destination returns false
  with zero bytes written and leaves the destination unchanged.
- `GetLegalMoves()` returns a read-only snapshot for the current side, including
  all promotion choices. It is empty after the game finishes. Move order is
  unspecified. `MakeMove` returns `MoveResult.None` for illegal moves or a
  finished game without changing state; successful moves switch the side to
  move, including a checkmating move. Use `Outcome.Winner`, not the side to
  move, to identify the winner.
- `GetPiece(Square)` reads one square. `CopyBoardTo(Span<ChessPiece>)` copies all
  64 squares in a1-to-h8 order. `History` is a read-only snapshot of successful
  moves with the move, FEN before/after and move flags. `UndoMove()` restores
  the previous position, repetition count and outcome, including after a
  terminal move or a draw claim made after that move. It returns false if there
  is no move to undo. A claim itself is not a move and cannot be undone when
  there is no earlier move.
- Assigning a `ChessGame` variable aliases the same game. `Clone()` makes an
  independent copy of position, history, repetition counts and outcome, even
  after a claim. Games are not thread-safe; use a separate clone for concurrent
  analysis. FEN contains a position, not history or a claimed outcome.

`MoveResult` describes a successful move (`Move`, `Capture`, `Check`,
`Checkmate`, `Stalemate`); `Outcome` describes the game result and finish
reason. Loaded positions are classified immediately. `CopyBoardTo` requires a
destination of at least 64 entries, with `ChessPiece.Empty` for vacant squares.
The raw bitboard position is deliberately not exposed; use FEN, board inspection,
legal moves, history and `Clone()` for consumer scenarios.

## Standard Algebraic Notation

`FormatSan` and `TryFormatSan` format a playable coordinate move in the current
position, before it is applied. `ParseSan` and `TryParseSan` resolve canonical
SAN to a `CoordinateMove` without applying it. `MakeSanMove` resolves and plays
the text through the same move application used by `MakeMove`, avoiding a second
search through the current position's legal moves. All SAN operations reject
finished games. The [SAN guide](san.md) describes exact notation rules, failure
behavior, examples and performance tradeoffs.

## Game record export and restoration

`ExportRecord()` creates an independent, immutable `GameRecord` containing the
game's initial FEN, its current line of successful coordinate moves, and a
successful draw claim when one ended the game. Undone moves are excluded;
continuing, undoing or cloning the game does not change an existing record.
The move collection owns its data and cannot be modified through collection
interfaces. A game created from FEN records only the history played since that
position; it cannot recover moves preceding the import.

```csharp
var game = new ChessGame();
game.MakeMove(CoordinateMove.Parse("e2e4"));
GameRecord record = game.ExportRecord();
ChessGame restored = ChessGame.FromRecord(record);
```

`record.DrawClaim` is null for ongoing games and automatic endings. A successful
threefold-repetition or fifty-move claim stores its reason and optional intended
move. That intended move is not part of `record.Moves`. A claim on the current
position has a null intended move. `ChessGame.DrawIntendedMove` exposes the
intended move of a successful claim, is copied by `Clone()`, and is cleared by a
successful `UndoMove()`. A failed undo leaves the claim unchanged.

`ChessGame.FromRecord(record)` creates an independent game from the initial FEN,
replays each move through `MakeMove`, then applies the optional claim through
`ClaimDraw`. This rebuilds history, repetition counts, available draw claims and
the outcome, including automatic endings. The intended move of a claim remains
unplayed. The restored game supports continued play and `UndoMove` with the same
rules as the original game.

A null record throws `ArgumentNullException`. An invalid initial FEN, an
unplayable move (including a move after the game has finished), or an invalid or
unavailable draw claim throws `FormatException`. Move errors identify the
zero-based index in `record.Moves`. Restoration returns a game only after the
whole record succeeds; it does not change the record or the original game.

## JSON game records

Use the static `GameRecord.Serialize` and `GameRecord.Deserialize` methods to
save and load records:

```csharp
string json = GameRecord.Serialize(game.ExportRecord());
GameRecord loaded = GameRecord.Deserialize(json);
ChessGame restored = ChessGame.FromRecord(loaded);
```

The version-one format is:

```json
{
  "schemaVersion": 1,
  "initialFen": "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1",
  "moves": ["e2e4", "e7e5"],
  "drawClaim": null
}
```

For a successful draw claim, `drawClaim` is an object such as:

```json
{
  "reason": "threefoldRepetition",
  "intendedMove": "f6g8"
}
```

That claim is valid only when the recorded history makes it available. The
other supported reason is `fiftyMoveRule`. A claim on the current position
writes `intendedMove: null`; an intended move remains outside `moves` and is
never played during restoration. Automatic endings write `drawClaim: null`.
Reasons are written in camel case and read using the standard case-insensitive
string-enum converter. Numeric reasons, `none` and combinations of claims are
invalid in a game record.

`schemaVersion`, `initialFen` and `moves` are required and cannot be null.
`drawClaim` may be null or omitted. Within a claim, `reason` is required and
`intendedMove` may be null or omitted. Property names are case sensitive;
property order and whitespace do not matter. Unknown and duplicate properties
are rejected, including inside a claim. Moves are lowercase coordinate strings,
including the explicit promotion suffix. Valid JSON escapes are accepted.

`GameRecord.SchemaVersion` is the library's constant for the supported format,
currently `1`. Serialization writes it into every document; callers do not
supply it. It is independent of the package version: package `1.1.0` does not
imply schema version `1.1.0`. A missing or unsupported schema version is rejected;
no migration of other versions is currently provided.

Deserialization checks the document and validates the entire game through the
same replay used by `ChessGame.FromRecord`, including draw claims. It returns an
immutable record only after all checks succeed. Calling `FromRecord` afterwards
replays it again to create the game to use. Invalid JSON, unsupported versions,
invalid FEN, unplayable moves and unavailable claims throw `JsonException`.
Replay errors retain the original `FormatException` as their inner exception;
move errors include their zero-based index. Null method arguments throw
`ArgumentNullException`.

The JSON document type and serializer options are internal. Use these static
methods for the versioned game-record format; direct
`JsonSerializer.Serialize(record)` is not the persistence API. Standalone
`Square` and `CoordinateMove` values do support `JsonSerializer` directly through
their converter attributes and serialize as string tokens such as `"e4"` and
`"e2e4"`. Invalid default values cannot be serialized. `DrawClaimRecord` uses the
standard object serializer with the converters for its reason and intended
move. No JSON-specific types are added to the public API.

## FEN validation

FEN import requires exactly six fields separated by single ASCII spaces, with
no surrounding whitespace. Placement must have eight complete ranks and valid
piece symbols; side, ordered castling rights and en passant square must be
well formed. Counters are nonnegative ASCII decimal integers, with fullmove at
least one. They use `BigInteger`, so large values are preserved; leading zeros
are normalized on export.

Validation also checks one king per side, nonadjacent kings, legal piece and
pawn counts (including promotion allowance), no check on the side that just
moved, castling rights backed by the relevant king and rook, and en passant
state consistent with a double push and a zero halfmove clock. An en passant
target does not require an adjacent capturing pawn. Export records a target
after every double push. Validation does not prove historical reachability.

Invalid `TryCreateFromFen` input returns false with a null game; it never
substitutes the starting position. `FromFen` throws `FormatException` for invalid
input. A FEN import begins with one occurrence and no move history. The standard
starting FEN is available as `ChessGame.StartingFen`.

## Draws and repetition

Threefold repetition and the 50-move rule make a draw *claimable*; they do not
finish the game automatically. Use `AvailableDrawClaims` and
`ClaimDraw(DrawClaim.ThreefoldRepetition)` or `ClaimDraw(DrawClaim.FiftyMoveRule)`.
Pass a legal intended move to `GetAvailableDrawClaims(move)` or
`ClaimDraw(reason, move)` to claim before playing it. A successful intended-move
claim leaves the board and history unchanged. Invalid or unavailable claims
return false without mutation.

Fivefold repetition and the 75-move rule end the game automatically. Checkmate
has priority if the final move also reaches the 75-move threshold. Repetition
compares placement, side to move, castling rights and the en passant square
only when a *legal* en passant capture exists; move counters are ignored. A
pinned pawn does not make en passant available for this purpose.

Dead-position detection covers king versus king, king plus one bishop or knight
versus king, and bishops-only positions where every bishop is on the same
square color. It does not attempt general dead-position proof, including
fortresses or blocked pawn structures. Lack of a forced mate alone does not
end a game.

The library supports standard board play, not resignation, draw agreement,
clocks, tournament claim procedures, PGN, Chess960, AI/search, UCI,
networking or a graphical interface.
