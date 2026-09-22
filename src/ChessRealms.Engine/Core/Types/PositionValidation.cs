using ChessRealms.Engine.Core.Constants;
using System.Numerics;

namespace ChessRealms.Engine.Core.Types;

/// <summary>Local standard-chess invariants, without proving historical reachability.</summary>
internal static class PositionValidation
{
    internal static bool IsValid(Position p)
    {
        if (!Colors.IsValid(p.color) || (p.castlings & ~Castlings.All) != 0
            || p.halfMoveClock < 0 || p.fullMoveCount < 1
            || (p.enpassant != Squares.Empty && !Squares.IsValid(p.enpassant))) return false;
        ulong all = 0;
        for (int color = 0; color < 2; color++)
        {
            ulong occupied = 0;
            for (int piece = 0; piece < 6; piece++)
            {
                ulong bb = p.pieceBBs[color * 6 + piece];
                if ((all & bb) != 0) return false;
                occupied |= bb;
                all |= bb;
            }
            if (occupied != p.blockers[color] || BitOperations.PopCount(occupied) > 16) return false;
            ulong pawns = p.pieceBBs[color * 6];
            int pawnCount = BitOperations.PopCount(pawns);
            if (pawnCount > 8 || (pawns & 0xff000000000000ffUL) != 0
                || BitOperations.PopCount(p.pieceBBs[color * 6 + Pieces.King]) != 1) return false;
            // Extra pieces require enough missing pawns to account for promotions.
            int promotions = System.Math.Max(0, BitOperations.PopCount(p.pieceBBs[color * 6 + Pieces.Knight]) - 2)
                + System.Math.Max(0, BitOperations.PopCount(p.pieceBBs[color * 6 + Pieces.Rook]) - 2)
                + System.Math.Max(0, BitOperations.PopCount(p.pieceBBs[color * 6 + Pieces.Queen]) - 1);
            ulong bishops = p.pieceBBs[color * 6 + Pieces.Bishop];
            promotions += System.Math.Max(0, BitOperations.PopCount(bishops & 0x55aa55aa55aa55aaUL) - 1)
                + System.Math.Max(0, BitOperations.PopCount(bishops & 0xaa55aa55aa55aa55UL) - 1);
            if (promotions > 8 - pawnCount) return false;
        }
        if (all != p.blockers[Colors.None]) return false;
        // Also excludes adjacent kings and positions where both kings are in check.
        if (p.IsKingChecked(Colors.Mirror(p.color))) return false;
        for (int i = 0; i < 4; i++)
        {
            if ((p.castlings & (1 << i)) == 0) continue;
            int color = i < 2 ? Colors.White : Colors.Black;
            int rank = i < 2 ? 0 : 56;
            if (p.GetPieceAt(rank + 4, color).Value != Pieces.King
                || p.GetPieceAt(rank + (i % 2 == 0 ? 7 : 0), color).Value != Pieces.Rook) return false;
        }
        if (p.enpassant != Squares.Empty)
        {
            int target = p.enpassant;
            int direction = p.color == Colors.White ? -8 : 8;
            if (target / 8 != (p.color == Colors.White ? 5 : 2)
                || (all & (1UL << target)) != 0 || (all & (1UL << (target - direction))) != 0
                || p.GetPieceAt(target + direction, Colors.Mirror(p.color)).Value != Pieces.Pawn
                || p.halfMoveClock != 0) return false;
        }
        return true;
    }
}
