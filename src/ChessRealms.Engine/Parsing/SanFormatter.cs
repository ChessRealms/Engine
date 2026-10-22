using ChessRealms.Engine.Core.Constants;
using ChessRealms.Engine.Core.Math;
using ChessRealms.Engine.Core.Movements;
using ChessRealms.Engine.Core.Types;

namespace ChessRealms.Engine.Parsing;

internal static class SanFormatter
{
    // Canonical SAN is at most seven ASCII characters, for example Qa6xb7#.
    internal const int MaxLength = 7;

    internal static int Write(Position before, int move, ReadOnlySpan<int> legalMoves, Span<char> text)
    {
        int length = 0;
        int castling = BinaryMoveOps.DecodeCastling(move);
        if (castling != Castlings.None)
        {
            ReadOnlySpan<char> castle = (castling & (Castlings.WK | Castlings.BK)) != 0 ? "O-O" : "O-O-O";
            castle.CopyTo(text);
            length = castle.Length;
        }
        else
        {
            int piece = BinaryMoveOps.DecodeSrcPiece(move);
            int source = BinaryMoveOps.DecodeSrc(move);
            int target = BinaryMoveOps.DecodeTrg(move);
            bool capture = BinaryMoveOps.DecodeCapture(move) != 0;
            if (piece != Pieces.Pawn)
            {
                text[length++] = "PNBRQK"[piece];
                bool alternative = false;
                bool sameFile = false;
                bool sameRank = false;
                foreach (int candidate in legalMoves)
                {
                    int otherSource = BinaryMoveOps.DecodeSrc(candidate);
                    if (otherSource == source || BinaryMoveOps.DecodeSrcPiece(candidate) != piece
                        || BinaryMoveOps.DecodeTrg(candidate) != target)
                    {
                        continue;
                    }

                    alternative = true;
                    sameFile |= otherSource % 8 == source % 8;
                    sameRank |= otherSource / 8 == source / 8;
                }

                if (alternative)
                {
                    if (!sameFile || sameRank)
                    {
                        text[length++] = (char)('a' + source % 8);
                    }

                    if (sameFile)
                    {
                        text[length++] = (char)('1' + source / 8);
                    }
                }
            }
            else if (capture)
            {
                text[length++] = (char)('a' + source % 8);
            }

            if (capture)
            {
                text[length++] = 'x';
            }

            text[length++] = (char)('a' + target % 8);
            text[length++] = (char)('1' + target / 8);
            int promotion = BinaryMoveOps.DecodePromotion(move);
            if (promotion != Promotions.None)
            {
                text[length++] = '=';
                text[length++] = "PNBRQK"[promotion];
            }
        }

        // SAN needs board geometry, not counters or repetition history. In
        // particular an automatic draw with check must not be mistaken for mate.
        var after = before;
        MoveDriver.MakeMove(ref after, move, updateCounters: false);
        after.SwitchColor();
        if (after.IsKingChecked(after.color))
        {
            Span<int> replies = stackalloc int[MoveGen.MaxMoves];
            text[length++] = LegalMoves.Write(after, replies) == 0 ? '#' : '+';
        }

        return length;
    }
}
