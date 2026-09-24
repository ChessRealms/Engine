using ChessRealms.Engine.Parsing;

namespace ChessRealms.Engine.Tests;

internal class ChessGameTests
{
    private static ChessPiece[] Board(ChessGame game)
    {
        var board = new ChessPiece[64];
        game.CopyBoardTo(board);
        return board;
    }

    private static void AssertPiece(ChessPiece[] board, string square, PieceColor color, PieceValue value)
        => Assert.That(board[Square.Parse(square).Index], Is.EqualTo(new ChessPiece(color, value)));

    [Test]
    public void NewGame_HasCompleteInitialBoardAndWhiteToMove()
    {
        ChessGame game = new();
        var board = Board(game!);
        PieceValue[] backRank = [PieceValue.Rook, PieceValue.Knight, PieceValue.Bishop, PieceValue.Queen,
            PieceValue.King, PieceValue.Bishop, PieceValue.Knight, PieceValue.Rook];
        Assert.Multiple(() =>
        {
            Assert.That(game!.CurrentColor, Is.EqualTo(PieceColor.White));
            Assert.That(game.OpponentColor, Is.EqualTo(PieceColor.Black));
            Assert.That(game.IsFinished, Is.False);
            Assert.That(game.HasLegalMoves, Is.True);
            for (int file = 0; file < 8; file++)
            {
                Assert.That(board[file], Is.EqualTo(new ChessPiece(PieceColor.White, backRank[file])));
                Assert.That(board[8 + file], Is.EqualTo(new ChessPiece(PieceColor.White, PieceValue.Pawn)));
                Assert.That(board[48 + file], Is.EqualTo(new ChessPiece(PieceColor.Black, PieceValue.Pawn)));
                Assert.That(board[56 + file], Is.EqualTo(new ChessPiece(PieceColor.Black, backRank[file])));
            }
            Assert.That(board[16..48], Is.All.EqualTo(ChessPiece.Empty));
        });
    }

    [Test]
    public void CreateFromFen_PreservesBoardAndBlackToMove()
    {
        Assert.That(ChessGame.TryCreateFromFen("4k3/8/8/8/8/8/4P3/4K3 b - - 0 1", out var game), Is.True);
        var board = Board(game!);
        Assert.Multiple(() =>
        {
            Assert.That(game!.CurrentColor, Is.EqualTo(PieceColor.Black));
            Assert.That(board.Count(piece => !piece.IsEmpty), Is.EqualTo(3));
            AssertPiece(board, "e8", PieceColor.Black, PieceValue.King);
            AssertPiece(board, "e1", PieceColor.White, PieceValue.King);
            AssertPiece(board, "e2", PieceColor.White, PieceValue.Pawn);
        });
    }

    [Test]
    public void OrdinaryMoves_UpdateBoardAndAlternateSides()
    {
        ChessGame game = new();
        Assert.That(game.MakeMove(CoordinateMove.Parse("e2e4")), Is.EqualTo(MoveResult.Move));
        Assert.That(game!.CurrentColor, Is.EqualTo(PieceColor.Black));
        var expected = Board(new ChessGame());
        expected[Square.Parse("e2").Index] = ChessPiece.Empty;
        expected[Square.Parse("e4").Index] = new(PieceColor.White, PieceValue.Pawn);
        Assert.That(Board(game), Is.EqualTo(expected));
        Assert.That(game.MakeMove(CoordinateMove.Parse("e7e5")), Is.EqualTo(MoveResult.Move));
        expected[Square.Parse("e7").Index] = ChessPiece.Empty;
        expected[Square.Parse("e5").Index] = new(PieceColor.Black, PieceValue.Pawn);
        Assert.That(Board(game), Is.EqualTo(expected));
        Assert.That(game!.CurrentColor, Is.EqualTo(PieceColor.White));
    }

    [Test]
    public void Capture_RemovesEnemyAndMovesAttacker()
    {
        ChessGame game = new();
        Assert.That(game.MakeMove(CoordinateMove.Parse("e2e4")), Is.EqualTo(MoveResult.Move));
        Assert.That(game.MakeMove(CoordinateMove.Parse("d7d5")), Is.EqualTo(MoveResult.Move));
        var expected = Board(game!);
        expected[Square.Parse("e4").Index] = ChessPiece.Empty;
        expected[Square.Parse("d5").Index] = new(PieceColor.White, PieceValue.Pawn);
        Assert.That(game.MakeMove(CoordinateMove.Parse("e4d5")), Is.EqualTo(MoveResult.Move | MoveResult.Capture));
        Assert.That(Board(game), Is.EqualTo(expected));
        Assert.That(game!.CurrentColor, Is.EqualTo(PieceColor.Black));
    }

    [TestCase(FenStrings.StartPosition, "e2e5")]
    [TestCase(FenStrings.StartPosition, "e7e5")]
    [TestCase(FenStrings.StartPosition, "a3a4")]
    [TestCase("k3r3/8/8/8/8/8/4R3/4K3 w - - 0 1", "e2f2")]
    public void IllegalMove_PreservesBoardTurnAndFinishedState(string fen, string move)
    {
        Assert.That(ChessGame.TryCreateFromFen(fen, out var game), Is.True);
        var before = Board(game!);
        var color = game!.CurrentColor;
        var finished = game.IsFinished;
        Assert.That(game.MakeMove(CoordinateMove.Parse(move)), Is.EqualTo(MoveResult.None));
        Assert.Multiple(() =>
        {
            Assert.That(Board(game), Is.EqualTo(before));
            Assert.That(game!.CurrentColor, Is.EqualTo(color));
            Assert.That(game.IsFinished, Is.EqualTo(finished));
        });
    }
}
