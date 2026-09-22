using ChessRealms.Engine.Core.Attacks;
using ChessRealms.Engine.Core.Constants;
using ChessRealms.Engine.Core.Types;

namespace ChessRealms.Engine.Core.Movements;

internal static unsafe class MoveGen
{
    // At most 16 pieces/side in validated standard positions. Each has at most
    // 27 queen destinations (a pawn has at most 12 promotion variants), plus
    // two castles. Span indexing also checks every write, even for internal fixtures.
    internal const int MaxMoves = 16 * 27 + 2;

    public static int WriteMoves(ref Position position, int color, Span<int> moves, int offset = 0)
    {
        int cursor = offset;

        cursor += PawnMovement.WriteMoves(ref position, color, moves, cursor);

        cursor += LeapingMovement.WriteMoves(
            ref position, color, Pieces.Knight,
            KnightAttacks.AttackMasksPtr, moves, cursor);

        cursor += SlidingMovement.WriteMoves(
            ref position, color, Pieces.Bishop,
            &BishopAttacks.GetSliderAttack, moves, cursor);

        cursor += SlidingMovement.WriteMoves(
            ref position, color, Pieces.Rook,
            &RookAttacks.GetSliderAttack, moves, cursor);

        cursor += SlidingMovement.WriteMoves(
            ref position, color, Pieces.Queen,
            &QueenAttacks.GetSliderAttack, moves, cursor);

        cursor += LeapingMovement.WriteMoves(
            ref position, color, Pieces.King,
            KingAttacks.AttackMasksPtr, moves, cursor);

        cursor += CastlingMovement.WriteMoves(ref position, color, moves, cursor);

        return cursor - offset;
    }
}
