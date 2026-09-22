using ChessRealms.Engine.Core.Attacks;
using ChessRealms.Engine.Core.Constants;
using ChessRealms.Engine.Core.Math;
using ChessRealms.Engine.Core.Movements;
using ChessRealms.Engine.Core.Types;
using ChessRealms.Engine.Parsing;

namespace ChessRealms.Engine.Tests.Core.MoveGeneration.Knight;

internal unsafe class KnightMovesTests
{
    //     ASCII Board (as white)
    //
    //   a b c d e f g h
    // 8 . . . . . . P .
    // 7 . . . p . . . P
    // 6 . . . . . n . .
    // 5 . . . p . . . P
    // 4 P . . . p . P .
    // 3 . . N . . . . .
    // 2 P . . . p . . .
    // 1 . P . p . . . .
    //   a b c d e f g h
    //
    private const string fen = "6P1/3p3P/5n2/3p3P/P3p1P1/2N5/P3p3/1P1p4 b - - 0 1";
    private Position position;

    public KnightMovesTests()
    {
        // Deliberately synthetic geometry fixture: no kings and pawns on back ranks.
        Assert.That(FenStrings.TryParseSyntax(fen, out position), Is.True);
    }

    [Test]
    public void Test_AsWhite()
    {
        const int color = Colors.White;
        const int expectedWritten = 5;
        Span<int> moves = stackalloc int[expectedWritten];
        int written = LeapingMovement.WriteMoves(
            ref position,
            color,
            Pieces.Knight,
            KnightAttacks.AttackMasksPtr,
            moves);

        Assert.That(written, Is.EqualTo(expectedWritten));

        HashSet<int> moveSet = moves[..written].ToArray().ToHashSet();

        int[] expectedMoves =
        [
            BinaryMoveOps.EncodeMove(
                Squares.c3, Pieces.Knight, color, Squares.b5),
            BinaryMoveOps.EncodeMove(
                Squares.c3, Pieces.Knight, color, Squares.d5, capture: 1),
            BinaryMoveOps.EncodeMove(
                Squares.c3, Pieces.Knight, color, Squares.e4, capture: 1),
            BinaryMoveOps.EncodeMove(
                Squares.c3, Pieces.Knight, color, Squares.e2, capture: 1),
            BinaryMoveOps.EncodeMove(
                Squares.c3, Pieces.Knight, color, Squares.d1, capture: 1),
        ];

        Assert.That(expectedMoves.All(moveSet.Contains), Is.True);
    }

    [Test]
    public void Test_AsBlack()
    {
        const int color = Colors.Black;
        const int expectedWritten = 5;
        Span<int> moves = stackalloc int[expectedWritten];
        int written = LeapingMovement.WriteMoves(
            ref position,
            Colors.Black,
            Pieces.Knight,
            KnightAttacks.AttackMasksPtr,
            moves);

        Assert.That(written, Is.EqualTo(expectedWritten));

        var moveSet = moves[..written].ToArray().ToHashSet();

        int[] expectedMoves =
        [
            BinaryMoveOps.EncodeMove(
                Squares.f6, Pieces.Knight, color, Squares.e8),
            BinaryMoveOps.EncodeMove(
                Squares.f6, Pieces.Knight, color, Squares.g8, capture: 1),
            BinaryMoveOps.EncodeMove(
                Squares.f6, Pieces.Knight, color, Squares.h7, capture: 1),
            BinaryMoveOps.EncodeMove(
                Squares.f6, Pieces.Knight, color, Squares.h5, capture: 1),
            BinaryMoveOps.EncodeMove(
                Squares.f6, Pieces.Knight, color, Squares.g4, capture: 1),
        ];

        Assert.That(expectedMoves.All(moveSet.Contains), Is.True);
    }
}
