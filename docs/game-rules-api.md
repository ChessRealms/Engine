# Game API and rule boundaries

`ChessGame` is a mutable, sealed standard-chess game. A new game starts at the
usual position; `TryCreateFromFen` starts from a supplied position. The game
generates legal moves, applies them, records successful moves and classifies
endings. Move generation uses bitboards and magic attack tables; pseudolegal
moves are filtered for king safety.

## Moves, state and ownership

- `AlgebraicMove.Parse` accepts lowercase coordinate moves such as `e2e4` and
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
- `Position` is a value snapshot. `History` is a read-only snapshot of successful
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
reason. Loaded positions are classified immediately. `GetBoardToSpan` requires
64 entries in a1-to-h8 order, with `ChessPiece.Empty` for vacant squares.

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
substitutes the starting position. `new ChessGame(position)` and
`FenStrings.Format(position)` throw `ArgumentException` for invalid positions.
A FEN import begins with one occurrence and no move history.

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
