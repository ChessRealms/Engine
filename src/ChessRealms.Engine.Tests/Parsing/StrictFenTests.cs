using ChessRealms.Engine.Core.Constants;
using ChessRealms.Engine.Core.Types;
using ChessRealms.Engine.Core.Movements;
using ChessRealms.Engine.Parsing;

namespace ChessRealms.Engine.Tests.Parsing;

internal class StrictFenTests
{
    [TestCase(null)]
    [TestCase("")]
    [TestCase("8/8/8/8/8/8/8 w - - 0 1")]
    [TestCase("8/8/8/8/8/8/8/8/8 w - - 0 1")]
    [TestCase("88888888 w - - 0 1")]
    [TestCase("7k/8/8/8/8/8/8/8K w - - 0 1")]
    [TestCase("7k/8/8/8/8/8/8/6K w - - 0 1")]
    [TestCase("7k/8/8/8/8/8/8/61K w - - 0 1")]
    [TestCase("7k/8/8/8/8/8/8/7K/ w - - 0 1")]
    [TestCase("7k/8/8/8/8/8/8/0K w - - 0 1")]
    [TestCase("7k/8/8/8/8/8/8/7X w - - 0 1")]
    [TestCase("7k/8/8/8/8/8/8/7K x - - 0 1")]
    [TestCase("7k/8/8/8/8/8/8/7K W - - 0 1")]
    [TestCase("7k/8/8/8/8/8/8/7K w - - 0 0")]
    [TestCase("7k/8/8/8/8/8/8/7K w - - -1 1")]
    [TestCase("7k/8/8/8/8/8/8/7K w - - 0 -1")]
    [TestCase("7k/8/8/8/8/8/8/7K w - - +1 1")]
    [TestCase("7k/8/8/8/8/8/8/7K w - - 0 +1")]
    [TestCase("7k/8/8/8/8/8/8/7K w - - 1.0 1")]
    [TestCase("7k/8/8/8/8/8/8/7K w - - ١ 1")]
    [TestCase("7k/8/8/8/8/8/8/7K w - - 0 1 extra")]
    [TestCase("7k/8/8/8/8/8/8/7K w - - 0 1 ")]
    [TestCase("7k/8/8/8/8/8/8/7K  w - - 0 1")]
    [TestCase("7k/8/8/8/8/8/8/7K\tw - - 0 1")]
    [TestCase("8/8/8/8/8/8/8/8 w - - 0 1")]
    [TestCase("7k/8/8/8/8/8/8/8 w - - 0 1")]
    [TestCase("7k/8/8/8/8/8/7K/7K w - - 0 1")]
    [TestCase("7k/7K/8/8/8/8/8/8 w - - 0 1")]
    [TestCase("7k/8/8/8/8/8/8/P6K w - - 0 1")]
    [TestCase("p6k/8/8/8/8/8/8/7K w - - 0 1")]
    [TestCase("7k/8/8/8/8/P7/PPPPPPPP/7K w - - 0 1")]
    [TestCase("7k/8/8/8/8/8/PPPPPPPP/QQ5K w - - 0 1")]
    [TestCase("7k/8/8/8/8/8/PPPPPPPP/2B1B2K w - - 0 1")]
    [TestCase("4k3/8/8/8/8/8/8/K3R3 w - - 0 1")]
    [TestCase("r3k3/8/8/8/8/8/8/K3R3 w - - 0 1")]
    [TestCase("4k3/8/8/8/8/8/8/4K3 w K - 0 1")]
    [TestCase("4k3/8/8/8/8/8/8/4K3 w Q - 0 1")]
    [TestCase("4k3/8/8/8/8/8/8/4K3 b k - 0 1")]
    [TestCase("4k3/8/8/8/8/8/8/4K3 b q - 0 1")]
    [TestCase("4k3/8/8/8/8/8/8/3K3R w K - 0 1")]
    [TestCase("4k3/8/8/8/8/8/8/4K2R w KK - 0 1")]
    [TestCase("4k3/8/8/8/8/8/8/R3K2R w QK - 0 1")]
    [TestCase("4k3/8/8/8/8/8/8/4K2R w K- - 0 1")]
    [TestCase("7k/8/8/3pP3/8/8/8/7K b - d6 0 1")]
    [TestCase("7k/8/8/8/3Pp3/8/8/7K w - d3 0 1")]
    [TestCase("7k/8/8/4P3/8/8/8/7K w - d6 0 1")]
    [TestCase("7k/8/3n4/3pP3/8/8/8/7K w - d6 0 1")]
    [TestCase("7k/3n4/8/3pP3/8/8/8/7K w - d6 0 1")]
    [TestCase("7k/8/8/3pP3/8/8/8/7K w - d6 1 1")]
    [TestCase("7k/8/8/3pP3/8/8/8/7K w - d5 0 1")]
    [TestCase("7k/8/8/3pP3/8/8/8/7K w - d6x 0 1")]
    public void InvalidFen_IsRejectedWithoutPartialPosition(string? fen)
    {
        Assert.That(FenStrings.TryParse(fen, out var position), Is.False);
        Assert.That(ChessGame.TryCreateFromFen(fen, out var game), Is.False);
        Assert.That(game, Is.Null);
        Assert.Throws<ArgumentException>(() => new ChessGame(position));
    }

    [TestCase("7k/8/8/8/8/8/8/7K w - - 9223372036854775808 1")]
    [TestCase("7k/8/8/8/8/8/8/7K w - - 0 9223372036854775808")]
    [TestCase("7k/8/8/8/8/8/8/7K w - - 999999999999999999999999999999999999 1")]
    [TestCase(FenStrings.StartPosition)]
    [TestCase("7k/8/8/3p4/8/8/8/7K w - d6 0 1")]
    [TestCase("7k/8/8/8/3P4/8/8/7K b - d3 0 1")]
    [TestCase("k3r3/8/8/3pP3/8/8/8/4K3 w - d6 0 1")]
    [TestCase("7k/8/8/8/8/8/8/7K w - - 9223372036854775807 9223372036854775807")]
    [TestCase("7k/8/8/8/8/8/8/7K b - - 2147483648 2147483648")]
    [TestCase("4k3/8/8/8/8/8/8/K3R3 b - - 0 1")]
    public void ValidFen_RoundTripsExactly(string fen)
    {
        Assert.That(FenStrings.TryParse(fen, out var position), Is.True);
        Assert.That(FenStrings.Format(position), Is.EqualTo(fen));
        Assert.That(new ChessGame(position).ToFen(), Is.EqualTo(fen));
    }

    [Test]
    public void Fen_LeadingZeroCountersAreCanonicalized()
    {
        Assert.That(FenStrings.TryParse("7k/8/8/8/8/8/8/7K w - - 000 001", out var p), Is.True);
        Assert.That(FenStrings.Format(p), Is.EqualTo("7k/8/8/8/8/8/8/7K w - - 0 1"));
    }

    [Test]
    public void PublicPositionEntryPoints_RejectMalformedState()
    {
        Assert.Throws<ArgumentException>(() => new ChessGame(default(Position)));
        Assert.Throws<ArgumentException>(() => FenStrings.Format(default));
        Assert.Throws<InvalidOperationException>(() => new Position().IsKingChecked());
        Assert.Throws<ArgumentOutOfRangeException>(() => Position.CreateDefault().GetPieceAt(64, Colors.White));
        Assert.Throws<ArgumentOutOfRangeException>(() => Position.CreateDefault().GetPieceAt(0, 3));
        Assert.Throws<ArgumentException>(() => new ChessGame().GetBoardToSpan(new ChessPiece[63]));
        var p = Position.CreateDefault();
        p.SetPieceAt(Squares.a2, Pieces.Queen, Colors.White);
        Assert.Throws<ArgumentException>(() => new ChessGame(p));
        p = WithInvalidBlockers();
        Assert.Throws<ArgumentException>(() => new ChessGame(p));
    }

    private static unsafe Position WithInvalidBlockers()
    {
        var p = Position.CreateDefault();
        p.blockers[0] = 0;
        return p;
    }

    [Test]
    public unsafe void BoundedMoveBuffer_ThrowsBeforeWritingOutsideSpan()
    {
        var p = Position.CreateDefault();
        var buffer = Enumerable.Repeat(-123, 4).ToArray();
        // A caller-provided short span remains safe even for a valid position.
        Assert.Throws<IndexOutOfRangeException>(() =>
        {
            var local = p;
            MoveGen.WriteMoves(ref local, local.color, buffer.AsSpan(1, 2));
        });
        Assert.That(buffer[0], Is.EqualTo(-123));
        Assert.That(buffer[3], Is.EqualTo(-123));
    }

    [Test]
    public unsafe void CastlingGenerator_DoesNotCreateAMissingRook_EvenForInternalMalformedState()
    {
        var p = Position.CreateDefault();
        p.PopPieceAt(Squares.h1, Pieces.Rook, Colors.White);
        p.PopPieceAt(Squares.f1, Pieces.Bishop, Colors.White);
        p.PopPieceAt(Squares.g1, Pieces.Knight, Colors.White);
        Span<int> moves = stackalloc int[2];
        Assert.That(CastlingMovement.WriteMoves(ref p, Colors.White, moves), Is.Zero);
    }
}
