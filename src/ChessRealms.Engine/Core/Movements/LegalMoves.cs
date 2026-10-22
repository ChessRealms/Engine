using ChessRealms.Engine.Core.Extensions;
using ChessRealms.Engine.Core.Math;
using ChessRealms.Engine.Core.Types;

namespace ChessRealms.Engine.Core.Movements;

// Board legality is shared by game play, SAN and validated PGN export. Game
// outcomes such as repetition draws are evaluated separately by ChessGame.
internal static class LegalMoves
{
    internal static int Write(Position position, Span<int> moves)
    {
        int written = MoveGen.WriteMoves(ref position, position.color, moves);
        int legalMoveCount = 0;
        for (int i = 0; i < written; i++)
        {
            var nextPosition = position;
            // Move legality is independent of FEN counters.
            MoveDriver.MakeMove(ref nextPosition, moves[i], updateCounters: false);
            if (!nextPosition.IsKingChecked(position.color))
            {
                moves[legalMoveCount++] = moves[i];
            }
        }

        return legalMoveCount;
    }

    internal static bool TryFind(CoordinateMove move, ReadOnlySpan<int> legalMoves, out int encodedMove)
    {
        encodedMove = 0;
        foreach (int candidate in legalMoves)
        {
            if (ToCoordinateMove(candidate) != move)
            {
                continue;
            }

            encodedMove = candidate;
            return true;
        }

        return false;
    }

    internal static CoordinateMove ToCoordinateMove(int encodedMove) => new(
        new Square(BinaryMoveOps.DecodeSrc(encodedMove)),
        new Square(BinaryMoveOps.DecodeTrg(encodedMove)),
        BinaryMoveOps.DecodePromotion(encodedMove).ToPublicPromotion());
}
