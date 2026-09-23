namespace ChessRealms.Engine.Tests;

internal class PublicApiSurfaceTests
{
    [Test]
    public void HighLevelApi_CoversFenBoardMovesAndSafeDefaults()
    {
        var game = ChessGame.FromFen(ChessGame.StartingFen);
        var e2 = Square.Parse("e2");

        Assert.Multiple(() =>
        {
            Assert.That(default(Square).IsValid, Is.False);
            Assert.That(default(ChessPiece), Is.EqualTo(ChessPiece.Empty));
            Assert.That(game.GetPiece(e2), Is.EqualTo(new ChessPiece(PieceColor.White, PieceValue.Pawn)));
            Assert.That(game.GetLegalMoves(), Does.Contain(CoordinateMove.Parse("e2e4")));
            Assert.That(game.Clone().ToFen(), Is.EqualTo(ChessGame.StartingFen));
        });
    }

    [Test]
    public void ExportedTypes_AreTheReviewedRootSurface()
    {
        string[] expected =
        [
            "ChessRealms.Engine.ChessGame",
            "ChessRealms.Engine.ChessPiece",
            "ChessRealms.Engine.CoordinateMove",
            "ChessRealms.Engine.DrawClaim",
            "ChessRealms.Engine.FinishReason",
            "ChessRealms.Engine.GameOutcome",
            "ChessRealms.Engine.GameResult",
            "ChessRealms.Engine.GameState",
            "ChessRealms.Engine.MoveHistoryEntry",
            "ChessRealms.Engine.MoveResult",
            "ChessRealms.Engine.PieceColor",
            "ChessRealms.Engine.PieceValue",
            "ChessRealms.Engine.Square"
        ];

        string[] actual = typeof(ChessGame).Assembly.GetExportedTypes()
            .Select(type => type.FullName!)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.That(actual, Is.EqualTo(expected));
    }
}
