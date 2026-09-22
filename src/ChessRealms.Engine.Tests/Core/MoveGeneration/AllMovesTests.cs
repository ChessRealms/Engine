using ChessRealms.Engine.Core.Movements;
using ChessRealms.Engine.Core.Types;
using ChessRealms.Engine.Parsing;

namespace ChessRealms.Engine.Tests.Core.MoveGeneration;

internal unsafe class AllMovesTests
{
    const string fen = "r3k2r/p1ppqpb1/bn1Ppnp1/4N3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R b KQkq - 1 1";

    [Test]
    public void Test1_AsBlack()
    {
        Assert.That(FenStrings.TryParse(fen, out Position position), Is.True);

        Span<int> moves = stackalloc int[MoveGen.MaxMoves];
        int written = MoveGen.WriteMoves(ref position, position.color, moves);

        Assert.That(written, Is.EqualTo(41));
    }
}
