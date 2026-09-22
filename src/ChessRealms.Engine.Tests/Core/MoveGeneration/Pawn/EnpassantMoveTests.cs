using ChessRealms.Engine.Core.Constants;
using ChessRealms.Engine.Core.Math;
using ChessRealms.Engine.Core.Movements;
using ChessRealms.Engine.Core.Types;
using ChessRealms.Engine.Parsing;

namespace ChessRealms.Engine.Tests.Core.MoveGeneration.Pawn;

internal unsafe class EnpassantMoveTests
{
    [Test]
    public void Test_1_AsWhite()
    {
        //     ASCII Board (as white)
        //
        // '*' - potential pawn moves
        //
        //   a b c d e f g h
        // 8 r n b q k b n r
        // 7 . p p p p . p .
        // 6 * * . . . . . .
        // 5 p P . . . . * .
        // 4 * . * * * p P p
        // 3 * . * * P * . *
        // 2 P . P P . P . P
        // 1 R N B Q K B N R
        //
        // total pawn moves: 13

        string fen = "rnbqkbnr/1pppp1p1/8/pP6/5pPp/4P3/P1PP1P1P/RNBQKBNR w KQkq a6 0 1";

        if (!FenStrings.TryParse(fen, out Position position))
        {
            Assert.Fail();
            return;
        }


        int us = Colors.White;

        #region Assert by moves count
        Span<int> moves = stackalloc int[40];
        int written = PawnMovement.WriteMoves(ref position, us, moves);
        int expectedLength = 13;

        Assert.That(written, Is.EqualTo(expectedLength));
        #endregion

        #region Assert by specified moves
        HashSet<int> moveSet = moves[..written].ToArray().ToHashSet();
        int expectedEnpassant = BinaryMoveOps.EncodeMove(
            Squares.b5, Pieces.Pawn, us, Squares.a6,
            capture: 1, enpassant: 1);

        Assert.That(moveSet, Does.Contain(expectedEnpassant));
        #endregion
    }

    [Test]
    public void Test_1_AsBlack()
    {
        //     ASCII Board (as white)
        //   a b c d e f g h
        // 8 r n b q k b n r
        // 7 . p p p p . p .
        // 6 . . . . . . . .
        // 5 p P . . . . . .
        // 4 . . . . . p P p
        // 3 . . . . P . . .
        // 2 P . P P . P . P
        // 1 R N B Q K B N R
        //
        // total pawn moves: 15

        string fen = "rnbqkbnr/1pppp1p1/8/pP6/5pPp/4P3/P1PP1P1P/RNBQKBNR b KQkq g3 0 1";

        if (!FenStrings.TryParse(fen, out Position position))
        {
            Assert.Fail();
            return;
        }



        int us = Colors.Black;

        #region Assert by moves count
        Span<int> moves = stackalloc int[40];
        int written = PawnMovement.WriteMoves(ref position, us, moves);
        int expectedLength = 15;

        Assert.That(written, Is.EqualTo(expectedLength));
        #endregion

        #region Assert by specified moves
        HashSet<int> moveSet = moves[..written].ToArray().ToHashSet();

        int expectedEnpassant1 = BinaryMoveOps.EncodeMove(
            Squares.f4, Pieces.Pawn, us, Squares.g3,
            capture: 1, enpassant: 1);

        int expectedEnpassant2 = BinaryMoveOps.EncodeMove(
            Squares.h4, Pieces.Pawn, us, Squares.g3,
            capture: 1, enpassant: 1);

        Assert.That(moveSet, Does.Contain(expectedEnpassant1));
        Assert.That(moveSet, Does.Contain(expectedEnpassant2));
        #endregion
    }
}
