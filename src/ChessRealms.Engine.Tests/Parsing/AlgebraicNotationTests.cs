using ChessRealms.Engine.Core.Constants;
using ChessRealms.Engine.Parsing;

namespace ChessRealms.Engine.Tests.Parsing;

internal class AlgebraicNotationTests
{
    [Test]
    public void ParseSquare_A4()
    {
        string a4 = "a4";
        int parsedSquare = AlgebraicNotation.ParseSquare(a4);
        Assert.That(parsedSquare, Is.EqualTo(Squares.a4));
    }

    [Test]
    public void ParseMove_A1H8()
    {
        string a1h8 = "a1h8";
        var move = AlgebraicNotation.ParseAlgebraicMove(a1h8);
        Assert.Multiple(() =>
        {
            Assert.That((int)move.Src, Is.EqualTo(Squares.a1));
            Assert.That((int)move.Trg, Is.EqualTo(Squares.h8));
        });
    }

    [Test]
    public void TryParseMove_A1H8_Succeed()
    {
        string a1h8 = "a1h8";
        bool parsed = AlgebraicNotation.TryParseAlgebraicMove(a1h8, out AlgebraicMove move);

        Assert.Multiple(() =>
        {
            Assert.That(parsed, Is.True);
            Assert.That((int)move.Src, Is.EqualTo(Squares.a1));
            Assert.That((int)move.Trg, Is.EqualTo(Squares.h8));
        });
    }

    [Test]
    public void TryParseMove_A1J3_Failed()
    {
        string a1h8 = "a1j3";
        bool parsed = AlgebraicNotation.TryParseAlgebraicMove(a1h8, out AlgebraicMove move);

        Assert.Multiple(() =>
        {
            Assert.That(parsed, Is.False);
            Assert.That((int)move.Src, Is.EqualTo(Squares.Empty));
            Assert.That((int)move.Trg, Is.EqualTo(Squares.Empty));
        });
    }
}
