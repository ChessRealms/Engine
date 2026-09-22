using ChessRealms.Engine.Core.Attacks;
using ChessRealms.Engine.Core.Constants;
using ChessRealms.Engine.Tests.Extensions;

namespace ChessRealms.Engine.Tests.Core.Attacks;

internal class BishopSliderAttackLookupTests
{
    [Test]
    public void From_E4()
    {
        // Expected Attacks at Board
        //   a b c d e f g h
        // 1 . . . . . . . .
        // 2 . . . . . . . .
        // 3 . . . * . * . .
        // 4 . . . . b . . .
        // 5 . . . * . * . .
        // 6 . . * . . . . .
        // 7 . * . . . . . .
        // 8 . . . . . . . .

        int attackFrom = Squares.e4;

        int[] blockerSquares =
        [
            Squares.d3,
            Squares.b7,
            Squares.f5,
            Squares.f3
        ];

        int[] expectedAttacks =
        [
            Squares.d3,
            Squares.b7,
            Squares.c6,
            Squares.d5,
            Squares.f5,
            Squares.f3
        ];

        ulong blockers = blockerSquares.ToBitboard();
        ulong attackMask = BishopAttacks.GetSliderAttack(attackFrom, blockers);
        ulong expected = expectedAttacks.ToBitboard();

        Assert.That(attackMask, Is.EqualTo(expected));
    }

    [Test]
    public void From_D4()
    {
        // Expected Attacks at Board
        //   a b c d e f g h
        // 1 . . . . . . . .
        // 2 . . . . . . . .
        // 3 . . * . * . . .
        // 4 . . . b . . . .
        // 5 . . * . * . . .
        // 6 . . . . . * . .
        // 7 . . . . . . . .
        // 8 . . . . . . . .

        int attackFrom = Squares.d4;

        int[] blockerSquares =
        [
            Squares.c3,
            Squares.e3,
            Squares.c5,
            Squares.f6
        ];

        int[] expectedAttacks =
        [
            Squares.c3,
            Squares.e3,
            Squares.c5,
            Squares.e5,
            Squares.f6
        ];

        ulong blockers = blockerSquares.ToBitboard();
        ulong attackMask = BishopAttacks.GetSliderAttack(attackFrom, blockers);
        ulong expected = expectedAttacks.ToBitboard();

        Assert.That(attackMask, Is.EqualTo(expected));
    }

    [Test]
    public void From_A1_NoBlockers()
    {
        // Expected Attacks at Board
        //   a b c d e f g h
        // 1 b . . . . . . .
        // 2 . * . . . . . .
        // 3 . . * . . . . .
        // 4 . . . * . . . .
        // 5 . . . . * . . .
        // 6 . . . . . * . .
        // 7 . . . . . . * .
        // 8 . . . . . . . *

        int attackFrom = Squares.a1;
        int[] expectedAttacks =
        [
            Squares.b2,
            Squares.c3,
            Squares.d4,
            Squares.e5,
            Squares.f6,
            Squares.g7,
            Squares.h8
        ];

        ulong blockers = 0UL;
        ulong attackMask = BishopAttacks.GetSliderAttack(attackFrom, blockers);
        ulong expected = expectedAttacks.ToBitboard();

        Assert.That(attackMask, Is.EqualTo(expected));
    }

    [Test]
    public void From_H1()
    {
        // Expected Attacks at Board
        //   a b c d e f g h
        // 1 . . . . . . . b
        // 2 . . . . . . * .
        // 3 . . . . . * . .
        // 4 . . . . * . . .
        // 5 . . . * . . . .
        // 6 . . . . . . . .
        // 7 . . . . . . . .
        // 8 . . . . . . . .

        int attackFrom = Squares.h1;

        int[] blockerSquares =
        [
            Squares.d5
        ];

        int[] expectedAttacks =
        [
            Squares.g2,
            Squares.f3,
            Squares.e4,
            Squares.d5
        ];

        ulong blockers = blockerSquares.ToBitboard();
        ulong attackMask = BishopAttacks.GetSliderAttack(attackFrom, blockers);
        ulong expected = expectedAttacks.ToBitboard();

        Assert.That(attackMask, Is.EqualTo(expected));
    }
}
