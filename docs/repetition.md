# Position repetition

Repetition tracking answers how often the current position has occurred in the
current game line. `ChessGame.RepetitionCount` includes the initial position.
For example, from the standard start, `Nf3 Nf6 Ng1 Ng8` returns to its second
occurrence. Playing the cycle again reaches the third occurrence, making a draw
claim available. The positions need not be consecutive or reached by the same
sequence of moves.

## What identifies a position

| Data | Included in repetition identity? |
| --- | --- |
| Piece types, colors and squares | Yes. |
| Side to move | Yes. |
| Castling rights | Yes, even if castling is currently blocked. |
| En passant target | Only if a legal en passant capture exists. |
| Halfmove clock and fullmove number | No. |

A rook returning to its original square does not regain a lost castling right.
An en passant target in FEN is insufficient: a pawn must actually be able to
capture there without leaving its own king in check. Thus a pinned pawn may
make that target irrelevant to repetition.

Threefold repetition permits a claim; fivefold repetition ends the game
automatically. The engine also supports a claim based on a legal intended move
that would produce the third occurrence, without playing that move. These
rules follow [FIDE Laws of Chess, articles 9.2 and 9.6](https://handbook.fide.com/chapter/e012023).
The separate 50/75-move rules use the halfmove clock, not repetition counts.
See [draw APIs and engine boundaries](game-rules-api.md#draws-and-repetition).

## How the engine counts

`RepetitionKey` is an internal immutable value containing the relevant position
data. `ChessGame` keeps the current key and a dictionary mapping each distinct
key to its number of occurrences in the current line:

1. Creating a game adds its initial position with count one.
2. Applying a move creates a key for the resulting position and increments that
   key's count. The resulting count feeds draw evaluation.
3. Undo decrements the departing position's count, removes a zero-count entry
   and reconstructs the current key from the restored undo position and its
   legal moves. That position uses its remaining count. An abandoned
   continuation does not affect a new branch.
4. Cloning copies the dictionary; further moves and undo are independent.
5. Checking an intended-move claim computes a candidate key and looks up its
   count plus one, without modifying the game or its dictionary.

The board encoding is exact. Three 64-bit bitplanes represent piece types on
all squares; a fourth marks black pieces. Occupancy follows from the type
planes, so white pieces are the occupied squares not marked black. A separate
integer stores the side, castling rights and normalized en passant target.
Legal moves already generated for the position determine whether en passant
belongs in the key; this does not require another legal-move pass.

`GetHashCode()` only selects where to look in the dictionary. Equality compares
all stored fields, so two different keys with the same hash remain different
positions. The engine does not use a bare probabilistic hash as position
identity. Keys are internal runtime values, not a serialization format or
stable cross-process identifier.

This avoids formatting, allocating and hashing a FEN string for every played
position. The dictionary still uses memory and can allocate when it grows.
Undo position snapshots and public history retain their own contracts;
repetition keys do not replace them. There is no separate per-ply key list:
undo already supplies the information needed to reconstruct the current key.

## Restoring games on a server

A final FEN alone cannot reveal how many times its position occurred earlier.
`FromFen` therefore starts with count one and no earlier history. Restoring a
`GameRecord` replays its line and reconstructs the counts. Use
`ChessGame.FromRecordJson(json)` for one validated replay directly from JSON.
Neither the dictionary nor its hashes need to be persisted with the record.

PGN export is different: its immutable model already owns a validated mainline
and result. The writer advances one position to generate canonical SAN and move
numbers, sharing legal-move and SAN code with game play. It does not need a new
history, undo stack or repetition dictionary. Import and `ToGame()` still
validate and reconstruct game state as described in the [PGN guide](pgn.md).
