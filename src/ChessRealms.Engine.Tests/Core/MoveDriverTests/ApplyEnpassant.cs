using ChessRealms.Engine.Core.Constants;
using ChessRealms.Engine.Core.Math;
using ChessRealms.Engine.Core.Movements;
using ChessRealms.Engine.Core.Types;
using ChessRealms.Engine.Parsing;

namespace ChessRealms.Engine.Tests.Core.MoveDriverTests;

internal class ApplyEnpassant
{
    [Test]
    public unsafe void Apply_EP_Black()
    {
        const string fen = "rnbqkbnr/p1pppppp/8/5P2/1pP5/8/PP1PP1PP/RNBQKBNR b KQkq c3 0 1";

        Assert.That(FenStrings.TryParse(fen, out Position position), Is.True);

        int epMove = BinaryMoveOps.EncodeMove(
            Squares.b4, Pieces.Pawn, Colors.Black, Squares.c3,
            enpassant: 1, capture: 1);

        MoveDriver.MakeMove(ref position, epMove);

        var srcPawn = position.GetPieceAt(Squares.c3, Colors.Black);
        var trgPawn = position.GetPieceAt(Squares.c4, Colors.White);

        Assert.Multiple(() =>
        {
            Assert.That(srcPawn.Color, Is.EqualTo(Colors.Black));
            Assert.That(srcPawn.Value, Is.EqualTo(Pieces.Pawn));
        });

        Assert.That(trgPawn, Is.EqualTo(Piece.Empty));
    }

    [Test]
    public void Apply_EP_White() 
    {
        const string fen = "rnbqkbnr/p1pppp1p/8/5Pp1/1pP5/8/PP1PP1PP/RNBQKBNR w KQkq g6 0 1";

        Assert.That(FenStrings.TryParse(fen, out Position position), Is.True);

        int epMove = BinaryMoveOps.EncodeMove(
            Squares.f5, Pieces.Pawn, Colors.White, Squares.g6,
            enpassant: 1, capture: 1);

        MoveDriver.MakeMove(ref position, epMove);

        var srcPawn = position.GetPieceAt(Squares.g6, Colors.White);
        var trgPawn = position.GetPieceAt(Squares.g5, Colors.Black);

        Assert.Multiple(() =>
        {
            Assert.That(srcPawn.Color, Is.EqualTo(Colors.White));
            Assert.That(srcPawn.Value, Is.EqualTo(Pieces.Pawn));
        });

        Assert.That(trgPawn, Is.EqualTo(Piece.Empty));
    }

    [Test]
    public void Apply_EP_White_H8()
    {
        const string fen = "rnbqkbnr/1ppppp1p/8/p5pP/8/8/PPPPPPP1/RNBQKBNR w KQkq g6 0 1";

        Assert.That(FenStrings.TryParse(fen, out Position position), Is.True);

        int epMove = BinaryMoveOps.EncodeMove(
            Squares.h5, Pieces.Pawn, Colors.White, Squares.g6,
            enpassant: 1, capture: 1);

        MoveDriver.MakeMove(ref position, epMove);

        var srcPawn = position.GetPieceAt(Squares.g6, Colors.White);
        var trgPawn = position.GetPieceAt(Squares.g5, Colors.Black);

        Assert.Multiple(() =>
        {
            Assert.That(srcPawn.Color, Is.EqualTo(Colors.White));
            Assert.That(srcPawn.Value, Is.EqualTo(Pieces.Pawn));
        });

        Assert.That(trgPawn, Is.EqualTo(Piece.Empty));
    }

    [Test]
    public void Apply_EP_Black_D3()
    {
        const string fen = "rnbqkbnr/p1p1pppp/5P2/8/1pPp4/8/PP1PP1PP/RNBQKBNR b KQkq c3 0 1";

        Assert.That(FenStrings.TryParse(fen, out Position position), Is.True);

        int epMove = BinaryMoveOps.EncodeMove(
            Squares.b4, Pieces.Pawn, Colors.Black, Squares.c3,
            enpassant: 1, capture: 1);

        MoveDriver.MakeMove(ref position, epMove);

        var srcPawn = position.GetPieceAt(Squares.c3, Colors.Black);
        var trgPawn = position.GetPieceAt(Squares.c4, Colors.White);

        Assert.Multiple(() =>
        {
            Assert.That(srcPawn.Color, Is.EqualTo(Colors.Black));
            Assert.That(srcPawn.Value, Is.EqualTo(Pieces.Pawn));
        });

        Assert.That(trgPawn, Is.EqualTo(Piece.Empty));
    }
}
