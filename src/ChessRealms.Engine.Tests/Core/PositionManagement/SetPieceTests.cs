using ChessRealms.Engine.Core.Constants;
using ChessRealms.Engine.Core.Types;

namespace ChessRealms.Engine.Tests.Core.PositionManagement;

internal class SetPieceTests
{
    [Test, Combinatorial]
    public void SetWhitePawn([Range(0, 63)] int square)
    {
        int color = Colors.White;
        int piece = Pieces.Pawn;

        Position position = new();
        position.SetPieceAt(square, piece, color);
        Piece actualPiece = position.GetPieceAt(square, color);

        Assert.Multiple(() =>
        {
            Assert.That(actualPiece.Color, Is.EqualTo(color));
            Assert.That(actualPiece.Value, Is.EqualTo(piece));
        });
    }

    [Test, Combinatorial]
    public void SetBlackPawn([Range(0, 63)] int square)
    {
        int color = Colors.Black;
        int piece = Pieces.Pawn;

        Position position = new();
        position.SetPieceAt(square, piece, color);
        Piece actualPiece = position.GetPieceAt(square, color);

        Assert.Multiple(() =>
        {
            Assert.That(actualPiece.Color, Is.EqualTo(color));
            Assert.That(actualPiece.Value, Is.EqualTo(piece));
        });
    }

    [Test, Combinatorial]
    public void SetWhiteKnight([Range(0, 63)] int square)
    {
        int color = Colors.White;
        int piece = Pieces.Knight;

        Position position = new();
        position.SetPieceAt(square, piece, color);
        Piece actualPiece = position.GetPieceAt(square, color);

        Assert.Multiple(() =>
        {
            Assert.That(actualPiece.Color, Is.EqualTo(color));
            Assert.That(actualPiece.Value, Is.EqualTo(piece));
        });
    }

    [Test, Combinatorial]
    public void SetBlackKnight([Range(0, 63)] int square)
    {
        int color = Colors.Black;
        int piece = Pieces.Knight;

        Position position = new();
        position.SetPieceAt(square, piece, color);
        Piece actualPiece = position.GetPieceAt(square, color);

        Assert.Multiple(() =>
        {
            Assert.That(actualPiece.Color, Is.EqualTo(color));
            Assert.That(actualPiece.Value, Is.EqualTo(piece));
        });
    }

    [Test, Combinatorial]
    public void SetWhiteBishop([Range(0, 63)] int square)
    {
        int color = Colors.White;
        int piece = Pieces.Bishop;

        Position position = new();
        position.SetPieceAt(square, piece, color);
        Piece actualPiece = position.GetPieceAt(square, color);

        Assert.Multiple(() =>
        {
            Assert.That(actualPiece.Color, Is.EqualTo(color));
            Assert.That(actualPiece.Value, Is.EqualTo(piece));
        });
    }

    [Test, Combinatorial]
    public void SetBlackBishop([Range(0, 63)] int square)
    {
        int color = Colors.Black;
        int piece = Pieces.Bishop;

        Position position = new();
        position.SetPieceAt(square, piece, color);
        Piece actualPiece = position.GetPieceAt(square, color);

        Assert.Multiple(() =>
        {
            Assert.That(actualPiece.Color, Is.EqualTo(color));
            Assert.That(actualPiece.Value, Is.EqualTo(piece));
        });
    }

    [Test, Combinatorial]
    public void SetWhiteRook([Range(0, 63)] int square)
    {
        int color = Colors.White;
        int piece = Pieces.Rook;

        Position position = new();
        position.SetPieceAt(square, piece, color);
        Piece actualPiece = position.GetPieceAt(square, color);

        Assert.Multiple(() =>
        {
            Assert.That(actualPiece.Color, Is.EqualTo(color));
            Assert.That(actualPiece.Value, Is.EqualTo(piece));
        });
    }

    [Test, Combinatorial]
    public void SetBlackRook([Range(0, 63)] int square)
    {
        int color = Colors.Black;
        int piece = Pieces.Rook;

        Position position = new();
        position.SetPieceAt(square, piece, color);
        Piece actualPiece = position.GetPieceAt(square, color);

        Assert.Multiple(() =>
        {
            Assert.That(actualPiece.Color, Is.EqualTo(color));
            Assert.That(actualPiece.Value, Is.EqualTo(piece));
        });
    }

    [Test, Combinatorial]
    public void SetWhiteQueen([Range(0, 63)] int square)
    {
        int color = Colors.White;
        int piece = Pieces.Queen;

        Position position = new();
        position.SetPieceAt(square, piece, color);
        Piece actualPiece = position.GetPieceAt(square, color);

        Assert.Multiple(() =>
        {
            Assert.That(actualPiece.Color, Is.EqualTo(color));
            Assert.That(actualPiece.Value, Is.EqualTo(piece));
        });
    }

    [Test, Combinatorial]
    public void SetBlackQueen([Range(0, 63)] int square)
    {
        int color = Colors.Black;
        int piece = Pieces.Queen;

        Position position = new();
        position.SetPieceAt(square, piece, color);
        Piece actualPiece = position.GetPieceAt(square, color);

        Assert.Multiple(() =>
        {
            Assert.That(actualPiece.Color, Is.EqualTo(color));
            Assert.That(actualPiece.Value, Is.EqualTo(piece));
        });
    }

    [Test, Combinatorial]
    public void SetWhiteKing([Range(0, 63)] int square)
    {
        int color = Colors.White;
        int piece = Pieces.King;

        Position position = new();
        position.SetPieceAt(square, piece, color);
        Piece actualPiece = position.GetPieceAt(square, color);

        Assert.Multiple(() =>
        {
            Assert.That(actualPiece.Color, Is.EqualTo(color));
            Assert.That(actualPiece.Value, Is.EqualTo(piece));
        });
    }

    [Test, Combinatorial]
    public void SetBlackKing([Range(0, 63)] int square)
    {
        int color = Colors.Black;
        int piece = Pieces.King;

        Position position = new();
        position.SetPieceAt(square, piece, color);
        Piece actualPiece = position.GetPieceAt(square, color);

        Assert.Multiple(() =>
        {
            Assert.That(actualPiece.Color, Is.EqualTo(color));
            Assert.That(actualPiece.Value, Is.EqualTo(piece));
        });
    }
}
