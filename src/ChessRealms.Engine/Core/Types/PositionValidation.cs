using ChessRealms.Engine.Core.Constants;
using System.Numerics;

namespace ChessRealms.Engine.Core.Types;

/// <summary>Local standard-chess invariants, without proving historical reachability.</summary>
internal static class PositionValidation
{
    internal static bool IsValid(Position position)
    {
        if (!Colors.IsValid(position.color) || (position.castlings & ~Castlings.All) != 0
            || position.halfMoveClock < 0 || position.fullMoveCount < 1
            || (position.enpassant != Squares.Empty && !Squares.IsValid(position.enpassant)))
        {
            return false;
        }

        ulong allOccupied = 0;
        for (int color = 0; color < 2; color++)
        {
            ulong occupiedByColor = 0;
            for (int piece = 0; piece < 6; piece++)
            {
                ulong pieceBoard = position.pieceBBs[color * 6 + piece];
                if ((allOccupied & pieceBoard) != 0)
                {
                    return false;
                }

                occupiedByColor |= pieceBoard;
                allOccupied |= pieceBoard;
            }

            if (occupiedByColor != position.blockers[color] || BitOperations.PopCount(occupiedByColor) > 16)
            {
                return false;
            }

            ulong pawns = position.pieceBBs[color * 6 + Pieces.Pawn];
            int pawnCount = BitOperations.PopCount(pawns);
            if (pawnCount > 8 || (pawns & (SquareMapping.RANK_1 | SquareMapping.RANK_8)) != 0
                || BitOperations.PopCount(position.pieceBBs[color * 6 + Pieces.King]) != 1)
            {
                return false;
            }

            // Extra pieces require enough missing pawns to account for promotions.
            int requiredPromotions = System.Math.Max(0, BitOperations.PopCount(position.pieceBBs[color * 6 + Pieces.Knight]) - 2)
                + System.Math.Max(0, BitOperations.PopCount(position.pieceBBs[color * 6 + Pieces.Rook]) - 2)
                + System.Math.Max(0, BitOperations.PopCount(position.pieceBBs[color * 6 + Pieces.Queen]) - 1);
            ulong bishops = position.pieceBBs[color * 6 + Pieces.Bishop];
            requiredPromotions += System.Math.Max(0, BitOperations.PopCount(bishops & SquareMapping.DARK_SQUARES) - 1)
                + System.Math.Max(0, BitOperations.PopCount(bishops & SquareMapping.LIGHT_SQUARES) - 1);
            if (requiredPromotions > 8 - pawnCount)
            {
                return false;
            }
        }

        if (allOccupied != position.blockers[Colors.None])
        {
            return false;
        }

        // Also excludes adjacent kings and positions where both kings are in check.
        if (position.IsKingChecked(Colors.Mirror(position.color)))
        {
            return false;
        }

        return HasValidCastlingRights(position) && HasValidEnPassant(position, allOccupied);
    }

    private static bool HasValidCastlingRights(Position position)
    {
        for (int i = 0; i < 4; i++)
        {
            if ((position.castlings & (1 << i)) == 0)
            {
                continue;
            }

            int color = i < 2 ? Colors.White : Colors.Black;
            int rankStart = i < 2 ? Squares.a1 : Squares.a8;
            int kingSquare = rankStart + 4;
            int rookSquare = rankStart + (i % 2 == 0 ? 7 : 0);
            if (position.GetPieceAt(kingSquare, color).Value != Pieces.King
                || position.GetPieceAt(rookSquare, color).Value != Pieces.Rook)
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasValidEnPassant(Position position, ulong allOccupied)
    {
        if (position.enpassant == Squares.Empty)
        {
            return true;
        }

        int target = position.enpassant;
        int direction = position.color == Colors.White ? Directions.South : Directions.North;
        int expectedRank = position.color == Colors.White ? 5 : 2;
        int pawnSource = target - direction;
        int pawnDestination = target + direction;
        if (target / 8 != expectedRank
            || (allOccupied & (1UL << target)) != 0 || (allOccupied & (1UL << pawnSource)) != 0
            || position.GetPieceAt(pawnDestination, Colors.Mirror(position.color)).Value != Pieces.Pawn
            || position.halfMoveClock != 0)
        {
            return false;
        }

        return true;
    }
}
