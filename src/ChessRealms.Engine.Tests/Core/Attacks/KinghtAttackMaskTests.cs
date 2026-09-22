using ChessRealms.Engine.Core.Attacks;
using ChessRealms.Engine.Core.Constants;
using ChessRealms.Engine.Tests.Extensions;

namespace ChessRealms.Engine.Tests.Core.Attacks;

internal class KinghtAttackMaskTests
{
    [Test]
    public void From_D5()
    {
        int attackFrom = Squares.d5;
        int[] attacks =
        [
            Squares.e7, Squares.c3,
            Squares.f6, Squares.b4,
            Squares.f4, Squares.b6,
            Squares.e3, Squares.c7
        ];

        ulong attackMask = KnightAttacks.AttackMasks[attackFrom];
        ulong expected = attacks.ToBitboard();

        Assert.That(attackMask, Is.EqualTo(expected));
    }

    [Test]
    public void From_A1()
    {
        int attackFrom = Squares.a1;
        int[] attacks =
        [
            Squares.b3,
            Squares.c2
        ];

        ulong attackMask = KnightAttacks.AttackMasks[attackFrom];
        ulong expected = attacks.ToBitboard();

        Assert.That(attackMask, Is.EqualTo(expected));
    }

    [Test]
    public void From_A8()
    {
        int attackFrom = Squares.a8;
        int[] attacks =
        [
            Squares.b6,
            Squares.c7
        ];

        ulong attackMask = KnightAttacks.AttackMasks[attackFrom];
        ulong expected = attacks.ToBitboard();

        Assert.That(attackMask, Is.EqualTo(expected));
    }

    [Test]
    public void From_H1()
    {
        int attackFrom = Squares.h1;
        int[] attacks =
        [
            Squares.f2,
            Squares.g3
        ];

        ulong attackMask = KnightAttacks.AttackMasks[attackFrom];
        ulong expected = attacks.ToBitboard();

        Assert.That(attackMask, Is.EqualTo(expected));
    }

    [Test]
    public void From_H8()
    {
        int attackFrom = Squares.h8;
        int[] attacks =
        [
            Squares.f7,
            Squares.g6
        ];
        
        ulong attackMask = KnightAttacks.AttackMasks[attackFrom];
        ulong expected = attacks.ToBitboard();

        Assert.That(attackMask, Is.EqualTo(expected));
    }
}
