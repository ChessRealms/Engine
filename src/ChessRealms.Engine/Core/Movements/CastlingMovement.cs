using ChessRealms.Engine.Core.Constants;
using ChessRealms.Engine.Core.Math;
using ChessRealms.Engine.Core.Types;

namespace ChessRealms.Engine.Core.Movements;

internal static class CastlingMovement
{
    public static int WriteMoves(ref Position position, int color, Span<int> dest, int offset = 0)
    {
        int cursor = offset;

        if (color == Colors.Black)
        {
            bool BK_CastlingAvailable = (position.castlings & Castlings.BK) != 0
                && position.GetPieceAt(Squares.e8, Colors.Black).Value == Pieces.King
                && position.GetPieceAt(Squares.h8, Colors.Black).Value == Pieces.Rook
                && BitboardOps.GetBitAt(position.blockers[Colors.None], Squares.f8) == 0
                && BitboardOps.GetBitAt(position.blockers[Colors.None], Squares.g8) == 0
                && !position.IsSquareAttackedByWhite(Squares.e8)
                && !position.IsSquareAttackedByWhite(Squares.f8)
                && !position.IsSquareAttackedByWhite(Squares.g8);

            if (BK_CastlingAvailable)
            {
                dest[cursor++] = BinaryMoveOps.EncodeMove(
                    Squares.e8, Pieces.King, Colors.Black, Squares.g8,
                    castling: Castlings.BK);
            }

            bool BQ_CastlingAvailable = (position.castlings & Castlings.BQ) != 0
                && position.GetPieceAt(Squares.e8, Colors.Black).Value == Pieces.King
                && position.GetPieceAt(Squares.a8, Colors.Black).Value == Pieces.Rook
                && BitboardOps.GetBitAt(position.blockers[Colors.None], Squares.b8) == 0
                && BitboardOps.GetBitAt(position.blockers[Colors.None], Squares.c8) == 0
                && BitboardOps.GetBitAt(position.blockers[Colors.None], Squares.d8) == 0
                && !position.IsSquareAttackedByWhite(Squares.e8)
                && !position.IsSquareAttackedByWhite(Squares.d8)
                && !position.IsSquareAttackedByWhite(Squares.c8);

            if (BQ_CastlingAvailable)
            {
                dest[cursor++] = BinaryMoveOps.EncodeMove(
                    Squares.e8, Pieces.King, Colors.Black, Squares.c8,
                    castling: Castlings.BQ);
            }
        }
        else
        {
            bool WK_CastlingAvailable = (position.castlings & Castlings.WK) != 0
                && position.GetPieceAt(Squares.e1, Colors.White).Value == Pieces.King
                && position.GetPieceAt(Squares.h1, Colors.White).Value == Pieces.Rook
                && BitboardOps.GetBitAt(position.blockers[Colors.None], Squares.f1) == 0
                && BitboardOps.GetBitAt(position.blockers[Colors.None], Squares.g1) == 0
                && !position.IsSquareAttackedByBlack(Squares.e1)
                && !position.IsSquareAttackedByBlack(Squares.f1)
                && !position.IsSquareAttackedByBlack(Squares.g1);

            if (WK_CastlingAvailable)
            {
                dest[cursor++] = BinaryMoveOps.EncodeMove(
                    Squares.e1, Pieces.King, Colors.White, Squares.g1,
                    castling: Castlings.WK);
            }

            bool WQ_CastlingAvailable = (position.castlings & Castlings.WQ) != 0
                && position.GetPieceAt(Squares.e1, Colors.White).Value == Pieces.King
                && position.GetPieceAt(Squares.a1, Colors.White).Value == Pieces.Rook
                && BitboardOps.GetBitAt(position.blockers[Colors.None], Squares.b1) == 0
                && BitboardOps.GetBitAt(position.blockers[Colors.None], Squares.c1) == 0
                && BitboardOps.GetBitAt(position.blockers[Colors.None], Squares.d1) == 0
                && !position.IsSquareAttackedByBlack(Squares.e1)
                && !position.IsSquareAttackedByBlack(Squares.d1)
                && !position.IsSquareAttackedByBlack(Squares.c1);

            if (WQ_CastlingAvailable)
            {
                dest[cursor++] = BinaryMoveOps.EncodeMove(
                    Squares.e1, Pieces.King, Colors.White, Squares.c1,
                    castling: Castlings.WQ);
            }
        }

        return cursor - offset;
    }
}
