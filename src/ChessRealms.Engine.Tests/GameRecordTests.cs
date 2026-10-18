namespace ChessRealms.Engine.Tests;

internal class GameRecordTests
{
    private const string RepetitionCycle = "g1f3 g8f6 f3g1 f6g8";

    [Test]
    public void ExportRecord_NewGameHasStartingFenAndNoMovesOrClaim()
    {
        var game = new ChessGame();
        var record = game.ExportRecord();

        Assert.Multiple(() =>
        {
            Assert.That(record.InitialFen, Is.EqualTo(ChessGame.StartingFen));
            Assert.That(record.Moves, Is.Empty);
            Assert.That(record.DrawClaim, Is.Null);
        });

        Play(game, "e2e4");
        Assert.That(record.Moves, Is.Empty);
    }

    [Test]
    public void ExportRecord_CustomStartAndCurrentLineSurviveCloningAndBranching()
    {
        const string fen = "4k3/8/8/8/8/8/8/R3K3 w - - 0 1";
        var game = ChessGame.FromFen(fen);
        Play(game, "a1a2 e8e7");
        var snapshot = game.ExportRecord();
        var clone = game.Clone();

        Assert.That(game.UndoMove(), Is.True);
        Play(game, "e8f7");
        var branch = game.ExportRecord();

        Assert.Multiple(() =>
        {
            Assert.That(snapshot.InitialFen, Is.EqualTo(fen));
            Assert.That(snapshot.Moves, Is.EqualTo(Moves("a1a2 e8e7")));
            Assert.That(branch.InitialFen, Is.EqualTo(fen));
            Assert.That(branch.Moves, Is.EqualTo(Moves("a1a2 e8f7")));
            Assert.That(clone.ExportRecord().InitialFen, Is.EqualTo(fen));
            Assert.That(clone.ExportRecord().Moves, Is.EqualTo(snapshot.Moves));
        });

        Assert.That(game.UndoMove(), Is.True);
        Assert.That(game.UndoMove(), Is.True);
        Assert.That(game.ExportRecord().InitialFen, Is.EqualTo(fen));
        Assert.That(game.ExportRecord().Moves, Is.Empty);
        Assert.That(branch.Moves, Is.EqualTo(Moves("a1a2 e8f7")));
    }

    [Test]
    public void Record_OwnsReadOnlyCopyOfCallerMoves()
    {
        var source = Moves("e2e4 e7e5").ToList();
        var record = new GameRecord(ChessGame.StartingFen, source, null);
        source[0] = CoordinateMove.Parse("d2d4");
        source.Clear();

        Assert.That(record.Moves, Is.EqualTo(Moves("e2e4 e7e5")));
        var exposed = (IList<CoordinateMove>)record.Moves;
        Assert.That(exposed.IsReadOnly, Is.True);
        Assert.Throws<NotSupportedException>(() => exposed.Clear());
        Assert.Throws<NotSupportedException>(() => exposed[0] = CoordinateMove.Parse("d2d4"));
        Assert.That(record.Moves, Is.EqualTo(Moves("e2e4 e7e5")));
    }

    [TestCase("q")]
    [TestCase("r")]
    [TestCase("b")]
    [TestCase("n")]
    public void ExportRecord_PreservesSelectedPromotion(string promotion)
    {
        const string fen = "7k/P7/8/8/8/8/8/7K w - - 0 1";
        var game = ChessGame.FromFen(fen);
        var move = CoordinateMove.Parse("a7a8" + promotion);
        Assert.That(game.MakeMove(move), Is.Not.EqualTo(MoveResult.None));

        var record = game.ExportRecord();
        Assert.That(record.InitialFen, Is.EqualTo(fen));
        Assert.That(record.Moves, Is.EqualTo(new[] { move }));
        Assert.That(record.DrawClaim, Is.Null);
    }

    [TestCase(ChessGame.StartingFen, RepetitionCycle + " " + RepetitionCycle,
        DrawClaim.ThreefoldRepetition, null)]
    [TestCase(ChessGame.StartingFen, RepetitionCycle + " g1f3 g8f6 f3g1",
        DrawClaim.ThreefoldRepetition, "f6g8")]
    [TestCase("4k3/8/8/8/8/8/8/R3K3 w - - 99 50", "a1a2",
        DrawClaim.FiftyMoveRule, null)]
    [TestCase("4k3/8/8/8/8/8/8/R3K3 w - - 99 50", "",
        DrawClaim.FiftyMoveRule, "a1a2")]
    public void ExportRecord_RecordsOnlySuccessfulClaimWithoutPlayingIntendedMove(
        string fen, string playedMoves, DrawClaim reason, string? intendedMoveText)
    {
        var game = ChessGame.FromFen(fen);
        Play(game, playedMoves);
        CoordinateMove? intendedMove = intendedMoveText is null
            ? null : CoordinateMove.Parse(intendedMoveText);
        string before = game.ToFen();
        var unclaimed = game.ExportRecord();

        Assert.That(game.ClaimDraw(reason, intendedMove), Is.True);
        var claimed = game.ExportRecord();

        Assert.Multiple(() =>
        {
            Assert.That(unclaimed.DrawClaim, Is.Null);
            Assert.That(claimed.DrawClaim, Is.EqualTo(new DrawClaimRecord(reason, intendedMove)));
            Assert.That(claimed.InitialFen, Is.EqualTo(fen));
            Assert.That(claimed.Moves, Is.EqualTo(Moves(playedMoves)));
            Assert.That(game.ToFen(), Is.EqualTo(before));
            Assert.That(game.DrawIntendedMove, Is.EqualTo(intendedMove));
        });

        // Rejected later claims must not replace the successful claim's details.
        Assert.That(game.ClaimDraw(reason), Is.False);
        Assert.That(game.ExportRecord().DrawClaim, Is.EqualTo(claimed.DrawClaim));
        Assert.That(game.DrawIntendedMove, Is.EqualTo(intendedMove));
    }

    [Test]
    public void IntendedClaim_ClonePreservesDetailsAndSuccessfulUndoClearsOnlyClone()
    {
        const string fen = "4k3/8/8/8/8/8/8/R3K3 w - - 98 50";
        var game = ChessGame.FromFen(fen);
        Play(game, "a1a2");
        var intendedMove = CoordinateMove.Parse("e8e7");
        Assert.That(game.ClaimDraw(DrawClaim.FiftyMoveRule, intendedMove), Is.True);
        var snapshot = game.ExportRecord();
        var clone = game.Clone();

        Assert.Multiple(() =>
        {
            Assert.That(clone.DrawIntendedMove, Is.EqualTo(intendedMove));
            Assert.That(clone.ExportRecord().InitialFen, Is.EqualTo(fen));
            Assert.That(clone.ExportRecord().Moves, Is.EqualTo(snapshot.Moves));
            Assert.That(clone.ExportRecord().DrawClaim, Is.EqualTo(snapshot.DrawClaim));
            Assert.That(clone.Outcome, Is.EqualTo(game.Outcome));
        });

        Assert.That(clone.UndoMove(), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(clone.DrawIntendedMove, Is.Null);
            Assert.That(clone.IsFinished, Is.False);
            Assert.That(clone.ToFen(), Is.EqualTo(fen));
            Assert.That(clone.ExportRecord().DrawClaim, Is.Null);
            Assert.That(clone.ExportRecord().Moves, Is.Empty);
            Assert.That(game.DrawIntendedMove, Is.EqualTo(intendedMove));
            Assert.That(game.ExportRecord().DrawClaim, Is.EqualTo(snapshot.DrawClaim));
        });

        Play(clone, "a1a3");
        Assert.That(clone.ExportRecord().DrawClaim, Is.Null);
        Assert.That(snapshot.DrawClaim, Is.EqualTo(new DrawClaimRecord(DrawClaim.FiftyMoveRule, intendedMove)));
        Assert.That(snapshot.Moves, Is.EqualTo(Moves("a1a2")));
    }

    [TestCase("4k3/8/8/8/8/8/8/R3K3 w - - 99 50", "a1a2")]
    [TestCase("4k3/8/8/8/8/8/8/R3K3 w - - 100 50", null)]
    public void ClaimWithoutHistory_FailedUndoPreservesClaim(string fen, string? intendedMoveText)
    {
        var game = ChessGame.FromFen(fen);
        CoordinateMove? intendedMove = intendedMoveText is null
            ? null : CoordinateMove.Parse(intendedMoveText);
        Assert.That(game.ClaimDraw(DrawClaim.FiftyMoveRule, intendedMove), Is.True);

        foreach (var copy in new[] { game, game.Clone() })
        {
            Assert.That(copy.UndoMove(), Is.False);
            Assert.That(copy.DrawIntendedMove, Is.EqualTo(intendedMove));
            Assert.That(copy.ExportRecord().DrawClaim,
                Is.EqualTo(new DrawClaimRecord(DrawClaim.FiftyMoveRule, intendedMove)));
            Assert.That(copy.ExportRecord().Moves, Is.Empty);
            Assert.That(copy.ToFen(), Is.EqualTo(fen));
            Assert.That(copy.Outcome.Reason, Is.EqualTo(FinishReason.FiftyMoveRule));
        }
    }

    [Test]
    public void RejectedClaims_DoNotBecomeRecordedClaims()
    {
        var game = ChessGame.FromFen("4k3/8/8/8/8/8/8/R3K3 w - - 99 50");
        Assert.That(game.ClaimDraw(DrawClaim.FiftyMoveRule), Is.False);
        Assert.That(game.ClaimDraw(DrawClaim.FiftyMoveRule, CoordinateMove.Parse("a1b2")), Is.False);
        Assert.That(game.ClaimDraw(DrawClaim.ThreefoldRepetition, CoordinateMove.Parse("a1a2")), Is.False);
        Assert.That(game.ClaimDraw(DrawClaim.ThreefoldRepetition | DrawClaim.FiftyMoveRule,
            CoordinateMove.Parse("a1a2")), Is.False);

        Assert.That(game.DrawIntendedMove, Is.Null);
        Assert.That(game.ExportRecord().DrawClaim, Is.Null);
        Assert.That(game.ExportRecord().Moves, Is.Empty);
        Assert.That(game.IsFinished, Is.False);
    }

    [TestCase("7k/6Q1/6K1/8/8/8/8/8 b - - 0 1", FinishReason.Checkmate)]
    [TestCase("7k/5Q2/6K1/8/8/8/8/8 b - - 0 1", FinishReason.Stalemate)]
    [TestCase("7k/8/8/8/8/8/8/7K w - - 0 1", FinishReason.DeadPosition)]
    [TestCase("4k3/8/8/8/8/8/8/R3K3 w - - 150 80", FinishReason.SeventyFiveMoveRule)]
    public void AutomaticEndings_AreNotExportedAsClaims(string fen, FinishReason reason)
    {
        var game = ChessGame.FromFen(fen);
        Assert.That(game.Outcome.Reason, Is.EqualTo(reason));
        Assert.That(game.ExportRecord().DrawClaim, Is.Null);
        Assert.That(game.ExportRecord().InitialFen, Is.EqualTo(fen));
    }

    [Test]
    public void FivefoldRepetition_PreservesAllMovesWithoutInventingClaim()
    {
        var game = new ChessGame();
        string playedMoves = string.Join(" ", Enumerable.Repeat(RepetitionCycle, 4));
        Play(game, playedMoves);

        Assert.That(game.Outcome.Reason, Is.EqualTo(FinishReason.FivefoldRepetition));
        var record = game.ExportRecord();
        Assert.That(record.InitialFen, Is.EqualTo(ChessGame.StartingFen));
        Assert.That(record.Moves, Is.EqualTo(Moves(playedMoves)));
        Assert.That(record.DrawClaim, Is.Null);
    }

    private static CoordinateMove[] Moves(string text) => text.Split(' ', StringSplitOptions.RemoveEmptyEntries)
        .Select(move => CoordinateMove.Parse(move)).ToArray();

    private static void Play(ChessGame game, string text)
    {
        foreach (var move in Moves(text))
            Assert.That(game.MakeMove(move), Is.Not.EqualTo(MoveResult.None), move.ToString());
    }
}
