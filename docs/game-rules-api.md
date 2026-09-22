# Standard game API

The engine implements standard chess board rules. It has no chess-engine runtime
dependency. Bitboards and magic attack tables remain the move-generation core.
A game owns its history; positions are copied values with inline bitboard arrays
and immutable `BigInteger` counters. No native memory copy is used for positions.

## Moves and legal-move snapshots

`AlgebraicMove` is an immutable value with `Source`, `Target`, and `Promotion`.
`Src` and `Trg` remain coordinate aliases. Squares are numbered a1 = 0 through
h8 = 63. Equality includes promotion.

`Parse` / `TryParse` accept exactly four lowercase coordinate characters, or five
with a lowercase `q`, `r`, `b`, or `n` suffix. They reject extra characters,
whitespace, uppercase, out-of-range squares and identical source/target.
`Parse` throws `FormatException`; `TryParse` returns false and `AlgebraicMove.Empty`.
Parsing checks representation; the game checks whether that move is legal.

**Promotion requires a suffix.** `a7a8` parses as an ordinary coordinate move but
is rejected when promotion is required. There is no implicit queen. A suffix on
an ordinary move, e.g. `e2e4q`, is also rejected by the game.

```csharp
using ChessRealms.Engine;

var game = new ChessGame();
var legalMoves = game.GetLegalMoves(); // read-only snapshot, initially 20
var branch = game.Clone();            // independent board, history and repetition counts
MoveResult result = branch.MakeMove(AlgebraicMove.Parse("e2e4"));
bool accepted = result != MoveResult.None;
string fen = branch.ToFen();          // ... b KQkq e3 0 1
branch.UndoMove();
bool restored = branch.Position == game.Position; // all bitboards and FEN fields
```

`GetLegalMoves()` returns every playable move for the current side, including all
four quiet/capture promotion variants. It never mutates the game. The ordering is
unspecified. It is empty after completion, including automatic and claimed draws.
`HasMoves()` follows the same contract.

`MakeMove` selects exactly one legal variant, computes the successor on a copy,
then commits it and one history entry. Invalid moves and moves after completion
return `MoveResult.None` without changing the board, counters, history, repetition
counts or outcome. Invalid manually constructed squares/promotion enum values are
also rejected. A legal move always switches side, even if it checkmates.

## State and completion

- `CurrentColor` / `EnemyColor`: the side to move and its opponent.
- `IsInCheck`: check on the current king, also available for a finished position.
- `State`: `Active`, `Check`, or `Finished`; `IsFinished` is the completion flag.
- `Outcome.Result`: `Ongoing`, `WhiteWin`, `BlackWin`, or `Draw`.
- `Outcome.Winner`: White/Black for checkmate; `PieceColor.None` otherwise.
- `Outcome.Reason`: `None`, `Checkmate`, `Stalemate`, `DeadPosition`,
  `ThreefoldRepetition`, `FiftyMoveRule`, `FivefoldRepetition`, or
  `SeventyFiveMoveRule`.

`MoveResult` is a description of a successful move, with flags for `Move`,
`Capture`, `Check`, `Checkmate`, and `Stalemate`. Checkmate includes `Check`.
Use `Outcome` for the game result; move flags do not encode every draw reason.
Loaded FENs are classified immediately, including mate, stalemate, supported dead
positions and a halfmove clock of at least 150.

The rules follow [FIDE Laws of Chess, effective 1 January 2023](https://handbook.fide.com/chapter/e012023),
articles 3, 5.1.1, 5.2 and 9.2–9.6. Checkmate is checked before automatic draws;
a mating 150th halfmove wins (9.6.2). If several draw conditions coincide,
classification prefers stalemate, then supported dead position, then fivefold
repetition, then the 75-move rule.

## Claims and repetitions

`AvailableDrawClaims` is a flags value: `None`, `ThreefoldRepetition`,
`FiftyMoveRule`, or both. Three occurrences or 100 halfmoves give the current
player the right to claim; play continues until a claim or another termination.
Five occurrences or 150 halfmoves end the game automatically.

```csharp
if (game.AvailableDrawClaims.HasFlag(DrawClaim.ThreefoldRepetition))
    game.ClaimDraw(DrawClaim.ThreefoldRepetition);

// FIDE also permits declaring an intended move before playing it.
var intended = AlgebraicMove.Parse("g1f3");
DrawClaim afterIntendedMove = game.GetAvailableDrawClaims(intended);
if (afterIntendedMove.HasFlag(DrawClaim.FiftyMoveRule))
    game.ClaimDraw(DrawClaim.FiftyMoveRule, intended);
```

Choose exactly one reason when claiming. Invalid/unavailable claims return false
without mutation. Intended-move queries simulate a legal move without playing it.
A successful intended-move claim ends the game **on the current board**, with no
new move/history entry. This models the claim before execution, rather than
playing a move and retroactively claiming for the previous player. If that move
is actually played instead, the normal checkmate/automatic-draw priority applies.

Repetition compares piece placement, side, castling rights, and en passant only
when at least one **legal** en passant capture exists. A pinned pawn or an en passant
capture that exposes the king does not distinguish the position. Counters do not
participate. Counts are exact string keys, not probabilistic hashes. A loaded FEN
starts at one occurrence with empty history; no prior occurrences are inferred.

## History, undo and ownership

`History` is a read-only snapshot of successful `MoveHistoryEntry` values:
move, FEN before/after and move flags. Existing snapshots do not change later.
`Position` is a value snapshot with structural equality over all 12 piece bitboards,
all 3 occupancy bitboards, side, castling, en passant and both counters.

`UndoMove()` returns false when there is no successful move to undo. Otherwise
it restores the complete previous position, removes the latest occurrence/history
entry and restores its outcome. It also clears any claim made after the move
being undone. Claims are not moves: a claim immediately after loading a FEN cannot
itself be undone by `UndoMove()`. Retain a `Clone()` to branch before claiming.

`ChessGame` is a sealed mutable class; ordinary C# assignment aliases the same
game. **Use `Clone()` for an independent game.** Clone copies all mutable collections
and the exact outcome, including a claimed draw. Games are not thread-safe; each
concurrent analysis should own a clone. FEN is a position interchange format,
not a history/outcome serialization format.

## FEN import and export

```csharp
using ChessRealms.Engine.Parsing;

if (ChessGame.TryCreateFromFen("7k/P7/8/8/8/8/8/7K w - - 0 1", out var promotionGame))
{
    promotionGame.MakeMove(AlgebraicMove.Parse("a7a8n"));
    Console.WriteLine(promotionGame.ToFen());
}
if (FenStrings.TryParse(FenStrings.StartPosition, out var position))
    Console.WriteLine(FenStrings.Format(position));
```

The strict grammar uses exactly six fields separated by single ASCII spaces,
without leading/trailing whitespace:

- Eight slash-separated ranks, each expanding to exactly eight squares.
  Only `pnbrqkPNBRQK` and digits 1–8; adjacent empty-run digits are rejected.
- Side `w` or `b`.
- `-` or a nonempty subsequence of `KQkq` in that order, without duplicates.
- `-` or an en passant square on rank 3/6.
- ASCII decimal integers: halfmove **>= 0**, fullmove **>= 1**. No sign,
  exponent, fraction, separators or Unicode digits. Leading zeros are accepted
  and removed on export. Counters use .NET `BigInteger`, with no fixed 32/64-bit
  numeric ceiling: large integers are preserved and incremented exactly rather
  than silently replaced with defaults on overflow. Practical input size is
  limited by available memory, as for any string.

Semantic validation requires:

- Exactly one king per color, nonadjacent kings, and the nonmoving king not in
  check. The current king may be checked or checkmated.
- Disjoint piece bitboards, consistent occupancy, at most 16 pieces and 8 pawns
  per color, no pawns on the first/eighth ranks. Extra queens/rooks/knights and
  extra bishops on either square color must fit the number of missing pawns.
- Every castling right requires its king on e1/e8 and its same-color rook on the
  corresponding corner. Blocked paths or attacked transit squares do not make
  the FEN invalid; they make castling unavailable.
- En passant rank must match the side to move, target and pawn origin must be
  empty, the opposing pawn must occupy the double-push destination, and halfmove
  must be zero. **An adjacent capturing pawn is not required.** Export records
  the target after every double push, including an uncapturable target.

No proof of historical reachability is attempted. Validation does not reconstruct
prior moves, captured material or check provenance, nor infer move history from
the counters. This intentionally permits locally consistent analysis positions.

Invalid input returns false and no partially populated position;
`TryCreateFromFen` returns **null**, not a fallback starting game.
`new ChessGame(position)` and `FenStrings.Format(position)` reject invalid
position values with `ArgumentException`. Public `Position.GetPieceAt` checks
square/color ranges; `Position.IsKingChecked()` rejects invalid positions.

## Supported dead positions and explicit limits

Automatic dead-position detection is deliberately sound and incomplete:

- King versus king.
- King and one bishop/knight versus king, either color.
- Kings with bishops only, when **all bishops occupy the same square color**,
  including promoted bishops.

It does not declare king and two knights versus king dead, nor opposing knights,
opposite-color bishops, or other material just because mate cannot be forced.
These cases can allow mate through cooperative legal play. General dead-position
proof, including blocked pawn structures or fortresses, is not implemented.
Such positions continue until another supported ending condition occurs.

This is a board-rules API, not a tournament arbiter. It does not implement
resignation, draw offers/agreement, clock/flag-fall, touch-move, scoresheets,
penalties or claim adjudication procedures. AI, UCI, SAN/PGN, Chess960, UI,
networking and search optimization are outside this stage.

## Safety and compatibility

All move destinations are written through bounds-checked `Span<int>`.
The full generator reserves **434** entries: at most 16 pieces per side, at most
27 destinations per piece (a pawn has at most 12 promotion variants), plus two
castles. This is a conservative bound for every accepted position, independent
of historical reachability. Short internal spans throw before an out-of-range
write. Internal synthetic geometric fixtures do not bypass public validation.

Breaking changes from the old API:

- `ChessGame` changed from struct to class; use `Clone()`, not assignment, to copy.
- `AlgebraicMove` now includes promotion and rejects formerly ignored suffixes.
- FEN parsing is strict; invalid `TryCreateFromFen` results are null.
- Terminal side-to-move is the side that would move next; use `Outcome.Winner`
  to display the winner.
- `FinishReason` was replaced with precise supported reasons; old unused
  `Draw`, `Mate`, `VoteForDraw`, and `Resign` members were removed.
- `HasMoves(PieceColor)` was removed: the game exposes its actual side's moves.
- Raw `Position` mutation/copy/attack helpers are internal; obtain valid public
  positions through FEN or `CreateDefault()`. `Position` remains a copied value,
  but is no longer unmanaged/blittable; native memory copying is unsupported.
- Counters exposed by the game are `BigInteger`; convert explicitly if a consumer
  requires a fixed-width integer and handle its numeric limits there.

The Console supports coordinate moves, `moves`, `fen`, `undo`,
`claim3 [move]`, `claim50 [move]`, and `quit`. It displays the explicit result,
winner and finish reason and allows undo after completion.
