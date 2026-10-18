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

Records currently remain in memory; JSON serialization is not implemented.
`GameRecord.SchemaVersion` identifies the record schema and is independent of
the package version.

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
clocks, tournament claim procedures, SAN/PGN, Chess960, AI/search, UCI,
networking or a graphical interface.
