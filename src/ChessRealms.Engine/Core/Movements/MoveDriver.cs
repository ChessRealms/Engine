using ChessRealms.Engine.Core.Constants;
using ChessRealms.Engine.Core.Extensions;
using ChessRealms.Engine.Core.Math;
using ChessRealms.Engine.Core.Types;
using ChessRealms.Engine.Debugs;

namespace ChessRealms.Engine.Core.Movements;

internal static class MoveDriver
{
    private static readonly int[] CastlingRightsLookup =
    [
        13, 15, 15, 15, 12, 15, 15, 14,
        15, 15, 15, 15, 15, 15, 15, 15,
        15, 15, 15, 15, 15, 15, 15, 15,
        15, 15, 15, 15, 15, 15, 15, 15,
        15, 15, 15, 15, 15, 15, 15, 15,
        15, 15, 15, 15, 15, 15, 15, 15,
        15, 15, 15, 15, 15, 15, 15, 15,
         7, 15, 15, 15,  3, 15, 15, 11
    ];

    public static void MakeMove(ref Position position, int move, bool updateCounters = true)
    {
        if (updateCounters)
        {
            position.halfMoveClock = BinaryMoveOps.DecodeSrcPiece(move) == Pieces.Pawn
                || BinaryMoveOps.DecodeCapture(move) != 0 ? 0 : position.halfMoveClock + 1;
            if (BinaryMoveOps.DecodeSrcColor(move) == Colors.Black)
                position.fullMoveCount = position.fullMoveCount + 1;
        }
        position.enpassant = Squares.Empty;

        int castling = BinaryMoveOps.DecodeCastling(move);
        if (castling != Castlings.None)
        {
            DebugHelper.Assert.IsValidSingleCastling(castling);

            switch (castling)
            {
                case Castlings.WK:
                    position.MovePiece(Squares.e1, Squares.g1, Colors.White, Pieces.King);
                    position.MovePiece(Squares.h1, Squares.f1, Colors.White, Pieces.Rook);
                    position.castlings ^= position.castlings & Castlings.White;
                    break;
                case Castlings.WQ:
                    position.MovePiece(Squares.e1, Squares.c1, Colors.White, Pieces.King);
                    position.MovePiece(Squares.a1, Squares.d1, Colors.White, Pieces.Rook);
                    position.castlings ^= position.castlings & Castlings.White;
                    break;
                case Castlings.BK:
                    position.MovePiece(Squares.e8, Squares.g8, Colors.Black, Pieces.King);
                    position.MovePiece(Squares.h8, Squares.f8, Colors.Black, Pieces.Rook);
                    position.castlings ^= position.castlings & Castlings.Black;
                    break;
                case Castlings.BQ:
                    position.MovePiece(Squares.e8, Squares.c8, Colors.Black, Pieces.King);
                    position.MovePiece(Squares.a8, Squares.d8, Colors.Black, Pieces.Rook);
                    position.castlings ^= position.castlings & Castlings.Black;
                    break;
            }
        }
        else if (BinaryMoveOps.DecodeEnpassant(move) != 0)
        {
            int src = BinaryMoveOps.DecodeSrc(move);
            int trg = BinaryMoveOps.DecodeTrg(move);
            int srcColor = BinaryMoveOps.DecodeSrcColor(move);

            position.MovePiece(src, trg, srcColor, Pieces.Pawn);

            int enemyPawnSquare = srcColor == Colors.White
                ? trg + Directions.South
                : trg + Directions.North;

            position.PopPieceAt(enemyPawnSquare, Pieces.Pawn, Colors.Mirror(srcColor));
        }
        else if (BinaryMoveOps.DecodeDoublePush(move) != 0)
        {
            int src = BinaryMoveOps.DecodeSrc(move);
            int trg = BinaryMoveOps.DecodeTrg(move);
            int srcColor = BinaryMoveOps.DecodeSrcColor(move);

            position.MovePiece(src, trg, srcColor, Pieces.Pawn);

            // Standard FEN records the target even when no capture is available.
            position.enpassant = (src + trg) / 2;
        }
        else
        {
            int src = BinaryMoveOps.DecodeSrc(move);
            int trg = BinaryMoveOps.DecodeTrg(move);
            int srcColor = BinaryMoveOps.DecodeSrcColor(move);
            int srcPiece = BinaryMoveOps.DecodeSrcPiece(move);
            int capture = BinaryMoveOps.DecodeCapture(move);

            if (capture.IsTrue())
            {
                position.PopPieceAt(trg, Colors.Mirror(srcColor));
            }

            position.MovePiece(src, trg, srcColor, srcPiece);

            if (srcPiece == Pieces.Pawn)
            {
                int promotion = BinaryMoveOps.DecodePromotion(move);
                if (promotion != Promotions.None)
                {
                    position.PopPieceAt(trg, srcPiece, srcColor);
                    position.SetPieceAt(trg, promotion, srcColor);
                }
            }

            position.castlings &= CastlingRightsLookup[src];
            position.castlings &= CastlingRightsLookup[trg];
        }
    }
}
