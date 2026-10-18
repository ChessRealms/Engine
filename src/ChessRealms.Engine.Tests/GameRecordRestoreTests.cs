namespace ChessRealms.Engine.Tests;

internal class GameRecordRestoreTests
{
    private const string RepetitionCycle = "g1f3 g8f6 f3g1 f6g8";
    private const string RookEnding = "4k3/8/8/8/8/8/8/R3K3 w - - ";

    [TestCase(ChessGame.StartingFen, "")]
    [TestCase(ChessGame.StartingFen, "e2e4 e7e5 g1f3 b8c6 f1b5 a7a6 b5c6")]
    [TestCase(ChessGame.StartingFen, "e2e4 a7a6 e4e5 d7d5 e5d6")]
    [TestCase("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1", "e1g1 e8c8")]
    [TestCase("7k/P7/8/8/8/8/8/7K w - - 0 1", "a7a8q")]
    [TestCase("7k/P7/8/8/8/8/8/7K w - - 0 1", "a7a8r")]
    [TestCase("7k/P7/8/8/8/8/8/7K w - - 0 1", "a7a8b")]
    [TestCase("7k/P7/8/8/8/8/8/7K w - - 0 1", "a7a8n")]
    [TestCase("4k3/8/8/8/8/8/8/R3K3 b - - 7 999999999999999999999", "e8e7 a1a2")]
    [TestCase(ChessGame.StartingFen, "f2f3 e7e5 g2g4 d8h4")]
    [TestCase(ChessGame.StartingFen, RepetitionCycle + " " + RepetitionCycle)]
    [TestCase(ChessGame.StartingFen,
        RepetitionCycle + " " + RepetitionCycle + " " + RepetitionCycle + " " + RepetitionCycle)]
    [TestCase(RookEnding + "100 50", "")]
    [TestCase(RookEnding + "149 80", "a1a2")]
    [TestCase("7k/6Q1/6K1/8/8/8/8/8 b - - 0 1", "")]
    [TestCase("7k/5Q2/6K1/8/8/8/8/8 b - - 0 1", "")]
    [TestCase("7k/8/8/8/8/8/8/7K w - - 0 1", "")]
    [TestCase(RookEnding + "150 80", "")]
    public void FromRecord_RebuildsStateHistoryAndUndo(string fen, string playedMoves)
    {
        var original = ChessGame.FromFen(fen);
        Play(original, playedMoves);
        var record = original.ExportRecord();

        var restored = ChessGame.FromRecord(record);

        Assert.That(restored, Is.Not.SameAs(original));
        AssertEquivalent(original, restored);
        AssertUndoEquivalent(original, restored);
        Assert.That(record.Moves, Is.EqualTo(Moves(playedMoves)));
    }

    [TestCase(ChessGame.StartingFen, RepetitionCycle + " " + RepetitionCycle,
        DrawClaim.ThreefoldRepetition, null)]
    [TestCase(ChessGame.StartingFen, RepetitionCycle + " g1f3 g8f6 f3g1",
        DrawClaim.ThreefoldRepetition, "f6g8")]
    [TestCase(RookEnding + "99 50", "a1a2", DrawClaim.FiftyMoveRule, null)]
    [TestCase(RookEnding + "98 50", "a1a2", DrawClaim.FiftyMoveRule, "e8e7")]
    [TestCase(RookEnding + "100 50", "", DrawClaim.FiftyMoveRule, null)]
    [TestCase(RookEnding + "99 50", "", DrawClaim.FiftyMoveRule, "a1a2")]
    public void FromRecord_RestoresClaimWithoutPlayingIntendedMove(
        string fen, string playedMoves, DrawClaim reason, string? intendedMoveText)
    {
        var original = ChessGame.FromFen(fen);
        Play(original, playedMoves);
        CoordinateMove? intendedMove = intendedMoveText is null
            ? null : CoordinateMove.Parse(intendedMoveText);
        string beforeClaim = original.ToFen();
        Assert.That(original.ClaimDraw(reason, intendedMove), Is.True);
        var record = original.ExportRecord();

        var restored = ChessGame.FromRecord(record);

        AssertEquivalent(original, restored);
        Assert.That(restored.ToFen(), Is.EqualTo(beforeClaim));
        Assert.That(restored.History.Select(entry => entry.Move), Is.EqualTo(Moves(playedMoves)));
        Assert.That(restored.DrawIntendedMove, Is.EqualTo(intendedMove));
        AssertUndoEquivalent(original, restored);
        Assert.That(record.DrawClaim, Is.EqualTo(new DrawClaimRecord(reason, intendedMove)));
    }

    [Test]
    public void FromRecord_ContinuesRepetitionHistoryWithIndependentOwnership()
    {
        var original = new ChessGame();
        Play(original, RepetitionCycle);
        var record = original.ExportRecord();
        var restored = ChessGame.FromRecord(record);
        var secondRestore = ChessGame.FromRecord(record);

        Play(restored, RepetitionCycle);
        Assert.That(restored.RepetitionCount, Is.EqualTo(3));
        Assert.That(restored.AvailableDrawClaims, Is.EqualTo(DrawClaim.ThreefoldRepetition));
        Assert.That(original.RepetitionCount, Is.EqualTo(2));
        AssertEquivalent(original, secondRestore);

        Assert.That(restored.UndoMove(), Is.True);
        Assert.That(restored.GetAvailableDrawClaims(CoordinateMove.Parse("f6g8")),
            Is.EqualTo(DrawClaim.ThreefoldRepetition));
        Play(restored, "f6g8 " + RepetitionCycle + " " + RepetitionCycle);
        Assert.That(restored.RepetitionCount, Is.EqualTo(5));
        Assert.That(restored.Outcome.Reason, Is.EqualTo(FinishReason.FivefoldRepetition));
        Assert.That(record.Moves, Is.EqualTo(Moves(RepetitionCycle)));
        AssertEquivalent(original, secondRestore);
    }

    [Test]
    public void FromRecord_NullRecordIsRejected()
    {
        var error = Assert.Throws<ArgumentNullException>(() => ChessGame.FromRecord(null!));
        Assert.That(error!.ParamName, Is.EqualTo("gameRecord"));
    }

    [TestCase("")]
    [TestCase("not a FEN")]
    [TestCase("8/8/8/8/8/8/8/8 w - - 0 1")]
    public void FromRecord_InvalidInitialFenIsRejected(string fen)
    {
        var record = new GameRecord(fen, [], null);

        var error = Assert.Throws<FormatException>(() => ChessGame.FromRecord(record));

        Assert.That(error!.Message, Does.Contain("initial FEN"));
    }

    [TestCase(ChessGame.StartingFen, "e2e5", 0)]
    [TestCase(ChessGame.StartingFen, "e2e4 d2d4", 1)]
    [TestCase(ChessGame.StartingFen, "f2f3 e7e5 g2g4 d8h4 a2a3", 4)]
    [TestCase(RookEnding + "149 80", "a1a2 e8e7", 1)]
    [TestCase(RookEnding + "150 80", "a1a2", 0)]
    [TestCase("7k/P7/8/8/8/8/8/7K w - - 0 1", "a7a8", 0)]
    public void FromRecord_UnplayableMoveIsRejectedWithItsIndex(string fen, string moves, int invalidIndex)
    {
        var record = new GameRecord(fen, Moves(moves), null);

        var error = Assert.Throws<FormatException>(() => ChessGame.FromRecord(record));

        Assert.That(error!.Message, Does.Contain($"index {invalidIndex}"));
        Assert.That(record.Moves, Is.EqualTo(Moves(moves)));
    }

    [Test]
    public void FromRecord_DefaultMoveIsRejectedAfterValidPrefix()
    {
        var record = new GameRecord(ChessGame.StartingFen, [CoordinateMove.Parse("e2e4"), default], null);

        var error = Assert.Throws<FormatException>(() => ChessGame.FromRecord(record));

        Assert.That(error!.Message, Does.Contain("index 1"));
    }

    [TestCase(RookEnding + "100 50", "", DrawClaim.None, null)]
    [TestCase(RookEnding + "100 50", "", DrawClaim.ThreefoldRepetition | DrawClaim.FiftyMoveRule, null)]
    [TestCase(RookEnding + "100 50", "", (DrawClaim)128, null)]
    [TestCase(RookEnding + "99 50", "", DrawClaim.FiftyMoveRule, null)]
    [TestCase(RookEnding + "99 50", "", DrawClaim.FiftyMoveRule, "a1b2")]
    [TestCase(RookEnding + "99 50", "", DrawClaim.ThreefoldRepetition, "a1a2")]
    [TestCase(ChessGame.StartingFen, RepetitionCycle, DrawClaim.ThreefoldRepetition, null)]
    [TestCase(ChessGame.StartingFen, "g1f3 g8f6 f3g1", DrawClaim.ThreefoldRepetition, "f6g8")]
    [TestCase(RookEnding + "149 80", "a1a2", DrawClaim.FiftyMoveRule, null)]
    public void FromRecord_InvalidOrUnavailableClaimIsRejected(
        string fen, string moves, DrawClaim reason, string? intendedMoveText)
    {
        CoordinateMove? intendedMove = intendedMoveText is null
            ? null : CoordinateMove.Parse(intendedMoveText);
        var claim = new DrawClaimRecord(reason, intendedMove);
        var record = new GameRecord(fen, Moves(moves), claim);

        var error = Assert.Throws<FormatException>(() => ChessGame.FromRecord(record));

        Assert.That(error!.Message, Does.Contain("draw claim"));
        Assert.That(record.DrawClaim, Is.EqualTo(claim));
        Assert.That(record.Moves, Is.EqualTo(Moves(moves)));
    }

    [Test]
    public void FromRecord_DefaultIntendedMoveIsRejected()
    {
        var claim = new DrawClaimRecord(DrawClaim.FiftyMoveRule, default(CoordinateMove));
        var record = new GameRecord(RookEnding + "100 50", [], claim);

        Assert.Throws<FormatException>(() => ChessGame.FromRecord(record));
    }

    private static void AssertEquivalent(ChessGame expected, ChessGame actual)
    {
        var expectedRecord = expected.ExportRecord();
        var actualRecord = actual.ExportRecord();
        Assert.Multiple(() =>
        {
            Assert.That(actual.ToFen(), Is.EqualTo(expected.ToFen()));
            Assert.That(actual.Outcome, Is.EqualTo(expected.Outcome));
            Assert.That(actual.State, Is.EqualTo(expected.State));
            Assert.That(actual.History, Is.EqualTo(expected.History));
            Assert.That(actual.RepetitionCount, Is.EqualTo(expected.RepetitionCount));
            Assert.That(actual.AvailableDrawClaims, Is.EqualTo(expected.AvailableDrawClaims));
            Assert.That(actual.DrawIntendedMove, Is.EqualTo(expected.DrawIntendedMove));
            Assert.That(actual.GetLegalMoves(), Is.EquivalentTo(expected.GetLegalMoves()));
            Assert.That(actualRecord.InitialFen, Is.EqualTo(expectedRecord.InitialFen));
            Assert.That(actualRecord.Moves, Is.EqualTo(expectedRecord.Moves));
            Assert.That(actualRecord.DrawClaim, Is.EqualTo(expectedRecord.DrawClaim));
        });
    }

    private static void AssertUndoEquivalent(ChessGame original, ChessGame restored)
    {
        int moveCount = original.History.Count;
        for (int i = 0; i < moveCount; i++)
        {
            Assert.That(original.UndoMove(), Is.True);
            Assert.That(restored.UndoMove(), Is.True);
            AssertEquivalent(original, restored);
        }

        Assert.That(original.UndoMove(), Is.False);
        Assert.That(restored.UndoMove(), Is.False);
        AssertEquivalent(original, restored);
    }

    private static CoordinateMove[] Moves(string text) => text.Split(' ', StringSplitOptions.RemoveEmptyEntries)
        .Select(move => CoordinateMove.Parse(move)).ToArray();

    private static void Play(ChessGame game, string text)
    {
        foreach (var move in Moves(text))
            Assert.That(game.MakeMove(move), Is.Not.EqualTo(MoveResult.None), move.ToString());
    }
}
