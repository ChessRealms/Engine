using ChessRealms.Engine.Core.Attacks;
using ChessRealms.Engine.Core.Constants;
using ChessRealms.Engine.Tests.Extensions;

namespace ChessRealms.Engine.Tests.Core.Attacks;

internal class KingAttackMaskTests
{
    [Test]
    public void From_D4()
    {
        int attackFrom = Squares.d4;
        int[] attacks =
        [
            Squares.c3, Squares.c4, Squares.c5,
            Squares.d3, Squares.d5,
            Squares.e3, Squares.e4, Squares.e5
        ];
        
        ulong attackMask = KingAttacks.AttackMasks[attackFrom];
        ulong expected = attacks.ToBitboard();

        Assert.That(attackMask, Is.EqualTo(expected));
    }

    [Test]
    public void From_A1()
    {
        int attackFrom = Squares.a1;
        int[] attacks =
        [
            Squares.a2,
            Squares.b1,
            Squares.b2
        ];
        
        ulong attackMask = KingAttacks.AttackMasks[attackFrom];
        ulong expected = attacks.ToBitboard();

        Assert.That(attackMask, Is.EqualTo(expected));
    }

    [Test]
    public void From_A8()
    {
        int attackFrom = Squares.a8;
        int[] attacks =
        [
            Squares.a7,
            Squares.b7,
            Squares.b8
        ];
        
        ulong attackMask = KingAttacks.AttackMasks[attackFrom];
        ulong expected = attacks.ToBitboard();

        Assert.That(attackMask, Is.EqualTo(expected));
    }

    [Test]
    public void From_H1()
    {
        int attackFrom = Squares.h1;
        int[] attacks =
        [
            Squares.h2,
            Squares.g1,
            Squares.g2
        ];

        ulong attackMask = KingAttacks.AttackMasks[attackFrom];
        ulong expected = attacks.ToBitboard();

        Assert.That(attackMask, Is.EqualTo(expected));
    }

    [Test]
    public void From_H8()
    {
        int attackFrom = Squares.h8;
        int[] attacks =
        [
            Squares.h7,
            Squares.g7,
            Squares.g8
        ];

        ulong attackMask = KingAttacks.AttackMasks[attackFrom];
        ulong expected = attacks.ToBitboard();

        Assert.That(attackMask, Is.EqualTo(expected));
    }
}
