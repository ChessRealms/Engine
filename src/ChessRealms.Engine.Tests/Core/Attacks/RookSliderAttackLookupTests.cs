using ChessRealms.Engine.Core.Attacks;
using ChessRealms.Engine.Core.Constants;
using ChessRealms.Engine.Tests.Extensions;

namespace ChessRealms.Engine.Tests.Core.Attacks;

internal class RookSliderAttackLookupTests
{
    [Test]
    public void From_E4()
    {
        // Expected Attacks at Board
        //
        //   a b c d e f g h
        // 1 . . . . * . . .
        // 2 . . . . * . . .
        // 3 . . . . * . . .
        // 4 . * * * r * * .
        // 5 . . . . * . . .
        // 6 . . . . . . . .
        // 7 . . . . . . . .
        // 8 . . . . . . . .

        int attackFrom = Squares.e4;

        int[] blockerSquares =
        [
            Squares.b4,
            Squares.g4,
            Squares.e5
        ];

        int[] expectedAttacks =
        [
            Squares.e1,
            Squares.e2,
            Squares.e3,
            Squares.e5,
            Squares.b4,
            Squares.c4,
            Squares.d4,
            Squares.f4,
            Squares.g4
        ];

        ulong blockers = blockerSquares.ToBitboard();
        ulong attackMask = RookAttacks.GetSliderAttack(attackFrom, blockers);
        ulong expected = expectedAttacks.ToBitboard();

        Assert.That(attackMask, Is.EqualTo(expected));
    }

    [Test]
    public void From_A1_NoBlockers()
    {
        // Expected Attacks at Board
        //
        //   a b c d e f g h
        // 1 r * * * * * * *
        // 2 * . . . . . . .
        // 3 * . . . . . . .
        // 4 * . . . . . . .
        // 5 * . . . . . . .
        // 6 * . . . . . . .
        // 7 * . . . . . . .
        // 8 * . . . . . . .

        int attackFrom = Squares.a1;

        int[] expectedAttacks =
        [
            Squares.a2, Squares.b1,
            Squares.a3, Squares.c1,
            Squares.a4, Squares.d1,
            Squares.a5, Squares.e1,
            Squares.a6, Squares.f1,
            Squares.a7, Squares.g1,
            Squares.a8, Squares.h1
        ];

        ulong blockers = 0UL;
        ulong attackMask = RookAttacks.GetSliderAttack(attackFrom, blockers);
        ulong expected = expectedAttacks.ToBitboard();

        Assert.That(attackMask, Is.EqualTo(expected));
    }

    [Test]
    public void From_H1()
    {
        // Expected Attacks at Board
        //
        //   a b c d e f g h
        // 1 . . . * * * * r
        // 2 . . . . . . . *
        // 3 . . . . . . . *
        // 4 . . . . . . . *
        // 5 . . . . . . . *
        // 6 . . . . . . . .
        // 7 . . . . . . . .
        // 8 . . . . . . . .

        int attackFrom = Squares.h1;

        int[] blockerSquares =
        [
            Squares.d1,
            Squares.h5
        ];

        int[] expectedAttacks =
        [
            Squares.d1, Squares.h2,
            Squares.e1, Squares.h3,
            Squares.f1, Squares.h4,
            Squares.g1, Squares.h5
        ];

        ulong blockers = blockerSquares.ToBitboard();
        ulong attackMask = RookAttacks.GetSliderAttack(attackFrom, blockers);
        ulong expected = expectedAttacks.ToBitboard();

        Assert.That(attackMask, Is.EqualTo(expected));
    }
}
