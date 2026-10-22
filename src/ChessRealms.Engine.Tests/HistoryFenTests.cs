using ChessRealms.Engine.Parsing;

namespace ChessRealms.Engine.Tests;

internal class HistoryFenTests
{
    private const string RepetitionLine = "g1f3 g8f6 f3g1 f6g8 g1f3 g8f6 f3g1 f6g8";

    [TestCase(ChessGame.StartingFen, "")]
    [TestCase(ChessGame.StartingFen, "e2e4 e7e5 g1f3 b8c6 f1b5 a7a6 b5c6")]
    [TestCase(ChessGame.StartingFen, "e2e4 a7a6 e4e5 d7d5 e5d6")]
    [TestCase("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1", "e1g1 e8c8")]
    [TestCase("7k/P7/8/8/8/8/8/7K w - - 0 1", "a7a8q")]
    [TestCase("7k/P7/8/8/8/8/8/7K w - - 0 1", "a7a8n")]
    [TestCase("4k3/8/8/8/8/8/8/R3K3 b - - 7 999999999999999999999", "e8e7 a1a2")]
    [TestCase(ChessGame.StartingFen, "f2f3 e7e5 g2g4 d8h4")]
    [TestCase(ChessGame.StartingFen, RepetitionLine)]
    [TestCase("4k3/8/8/8/8/8/8/R3K3 w - - 149 80", "a1a2")]
    public void DeferredHistoryMatchesEveryPositionAndMoveFlag(string initialFen, string line)
    {
        var game = ChessGame.FromFen(initialFen);
        var moves = ParseLine(line);
        var fens = new List<string> { FenFormatter.Format(game.Position) };
        var results = new List<MoveResult>();
        foreach (var move in moves)
        {
            var result = game.MakeMove(move);
            Assert.That(result, Is.Not.EqualTo(MoveResult.None));
            results.Add(result);
            // Build expectations without querying the game's lazy FEN or history APIs.
            fens.Add(FenFormatter.Format(game.Position));
        }

        var clone = game.Clone();
        // Exercise materializing a whole snapshot before any individual FEN queries.
        _ = clone.History;
        AssertLine(clone, fens, moves, results);
        AssertLine(game, fens, moves, results);
        var snapshot = game.History;
        var expectedSnapshot = snapshot.ToArray();
        for (int count = moves.Length - 1; count >= 0; count--)
        {
            Assert.That(game.UndoMove(), Is.True);
            AssertLine(game, fens.Take(count + 1).ToArray(), moves[..count], results.Take(count).ToArray());
            Assert.That(snapshot, Is.EqualTo(expectedSnapshot));
        }

        Assert.That(game.UndoMove(), Is.False);
        Assert.That(game.PlyCount, Is.Zero);
        AssertLine(clone, fens, moves, results);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void SnapshotsAndColdOrWarmClonesSurviveUndoAndDifferentContinuations(bool readBeforeClone)
    {
        var game = new ChessGame();
        Play(game, "e2e4 e7e5 g1f3 b8c6");
        if (readBeforeClone)
        {
            _ = game.History;
        }

        var clone = game.Clone();
        var snapshot = game.History;
        var expectedSnapshot = snapshot.ToArray();
        string unchangedPrefix = game.GetFenAtPly(2);

        Assert.That(game.UndoMove(), Is.True);
        Assert.That(game.UndoMove(), Is.True);
        Assert.That(game.ToFen(), Is.SameAs(unchangedPrefix));
        Play(game, "f1c4");
        Assert.That(game.PlyCount, Is.EqualTo(3));
        Assert.That(game.ToFen(), Is.EqualTo(FenFormatter.Format(game.Position)));
        Assert.That(game.GetFenAtPly(3), Is.Not.EqualTo(snapshot[2].FenAfter));
        Assert.Throws<ArgumentOutOfRangeException>(() => game.GetFenAtPly(4));

        Assert.That(clone.UndoMove(), Is.True);
        Play(clone, "d7d6");
        Assert.That(clone.PlyCount, Is.EqualTo(4));
        Assert.That(clone.ToFen(), Is.EqualTo(FenFormatter.Format(clone.Position)));
        Assert.That(clone.GetFenAtPly(3), Is.EqualTo(snapshot[2].FenAfter));
        Assert.That(clone.GetFenAtPly(4), Is.Not.EqualTo(snapshot[3].FenAfter));
        Assert.That(game.GetFenAtPly(2), Is.SameAs(unchangedPrefix));
        Assert.That(snapshot, Is.EqualTo(expectedSnapshot));
        Assert.Throws<NotSupportedException>(() => ((IList<MoveHistoryEntry>)snapshot)[0] = default);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void HistoricalFenQueriesAndRejectedMovesPreserveDrawClaims(bool intended)
    {
        var game = new ChessGame();
        Play(game, string.Join(' ', ParseLine(RepetitionLine).Take(intended ? 7 : 8)));
        CoordinateMove? intendedMove = intended ? CoordinateMove.Parse("f6g8") : null;
        Assert.That(game.ClaimDraw(DrawClaim.ThreefoldRepetition, intendedMove), Is.True);
        var before = game.Clone();
        string current = game.ToFen();

        Span<char> buffer = stackalloc char[128];
        for (int ply = 0; ply <= game.PlyCount; ply++)
        {
            Assert.That(game.TryFormatFenAtPly(ply, buffer, out int written), Is.True);
            Assert.That(new string(buffer[..written]), Is.EqualTo(game.GetFenAtPly(ply)));
        }

        Assert.That(game.MakeMove(CoordinateMove.Parse("g1f3")), Is.EqualTo(MoveResult.None));
        Assert.That(game.MakeSanMove("Nf3"), Is.EqualTo(MoveResult.None));
        Assert.Multiple(() =>
        {
            Assert.That(game.ToFen(), Is.SameAs(current));
            Assert.That(game.Position, Is.EqualTo(before.Position));
            Assert.That(game.Outcome, Is.EqualTo(before.Outcome));
            Assert.That(game.DrawIntendedMove, Is.EqualTo(before.DrawIntendedMove));
            Assert.That(game.RepetitionCount, Is.EqualTo(before.RepetitionCount));
            Assert.That(game.History, Is.EqualTo(before.History));
        });
        Assert.That(game.UndoMove(), Is.True);
        Assert.That(before.UndoMove(), Is.True);
        Assert.That(game.ToFen(), Is.EqualTo(before.ToFen()));
        Assert.That(game.DrawIntendedMove, Is.Null);
    }

    [Test]
    public void ClaimWithoutHistoryAndFailedUndoKeepInitialPosition()
    {
        var game = ChessGame.FromFen("4k3/8/8/8/8/8/8/R3K3 w - - 99 50");
        string initial = game.GetFenAtPly(0);
        Assert.That(game.ClaimDraw(DrawClaim.FiftyMoveRule, CoordinateMove.Parse("a1a2")), Is.True);
        var outcome = game.Outcome;
        Assert.That(game.UndoMove(), Is.False);
        Assert.That(game.PlyCount, Is.Zero);
        Assert.That(game.GetFenAtPly(0), Is.SameAs(initial));
        Assert.That(game.History, Is.Empty);
        Assert.That(game.Outcome, Is.EqualTo(outcome));
    }

    [Test]
    public void RejectedMoveBeforeGameEndsDoesNotAddOrInvalidateHistory()
    {
        var game = new ChessGame();
        Play(game, "e2e4");
        string current = game.ToFen();
        var snapshot = game.History;
        Assert.That(game.MakeMove(CoordinateMove.Parse("e2e5")), Is.EqualTo(MoveResult.None));
        Assert.That(game.MakeSanMove("e5+"), Is.EqualTo(MoveResult.None));
        Assert.That(game.PlyCount, Is.EqualTo(1));
        Assert.That(game.ToFen(), Is.SameAs(current));
        Assert.That(game.History, Is.EqualTo(snapshot));
    }

    [TestCase(-1)]
    [TestCase(2)]
    [TestCase(int.MinValue)]
    [TestCase(int.MaxValue)]
    public void OutOfRangePlyHasExplicitFailureAndLeavesBufferUnchanged(int ply)
    {
        var game = new ChessGame();
        Play(game, "e2e4");
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => game.GetFenAtPly(ply));
        Assert.That(exception!.ParamName, Is.EqualTo("ply"));
        char[] buffer = Enumerable.Repeat('?', 128).ToArray();
        Assert.That(game.TryFormatFenAtPly(ply, buffer.AsSpan(2, 100), out int written), Is.False);
        Assert.That(written, Is.Zero);
        Assert.That(buffer, Is.All.EqualTo('?'));
        Assert.That(game.TryFormatFenAtPly(ply, Span<char>.Empty, out written), Is.False);
        Assert.That(written, Is.Zero);
        Assert.That(game.PlyCount, Is.EqualTo(1));
    }

    [Test]
    public void LargeHistoricalCountersUseTheCorrectStateAndRespectSpanBoundaries()
    {
        string fullmove = new('9', 4096);
        var game = ChessGame.FromFen($"4k3/8/8/8/8/8/8/R3K3 b - - 7 {fullmove}");
        var expected = new List<string> { FenFormatter.Format(game.Position) };
        foreach (var move in ParseLine("e8e7 a1a2"))
        {
            Assert.That(game.MakeMove(move), Is.Not.EqualTo(MoveResult.None));
            expected.Add(FenFormatter.Format(game.Position));
        }

        for (int ply = 0; ply < expected.Count; ply++)
        {
            AssertSpanAtPly(game, ply, expected[ply]);
            Assert.That(game.GetFenAtPly(ply), Is.EqualTo(expected[ply]));
        }
    }

    [Test]
    public void PublicHistoryEntriesKeepConstructorDeconstructionAndValueSemantics()
    {
        var game = new ChessGame();
        var move = CoordinateMove.Parse("e2e4");
        var flags = game.MakeMove(move);
        var entry = game.History[0];
        var expected = new MoveHistoryEntry(move, ChessGame.StartingFen, game.ToFen(), flags);
        Assert.That(entry, Is.EqualTo(expected));
        Assert.That(entry.GetHashCode(), Is.EqualTo(expected.GetHashCode()));
        var (actualMove, before, after, result) = entry;
        Assert.That((actualMove, before, after, result), Is.EqualTo((move, ChessGame.StartingFen, game.ToFen(), flags)));
        var editedCopy = entry with { FenAfter = "consumer text" };
        Assert.That(editedCopy, Is.Not.EqualTo(entry));
        Assert.That(game.History[0], Is.EqualTo(expected));
    }

    private static void AssertLine(ChessGame game, IReadOnlyList<string> fens,
        IReadOnlyList<CoordinateMove> moves, IReadOnlyList<MoveResult> results)
    {
        Assert.That(game.PlyCount, Is.EqualTo(moves.Count));
        for (int ply = moves.Count; ply >= 0; ply--)
        {
            AssertSpanAtPly(game, ply, fens[ply]);
            string fen = game.GetFenAtPly(ply);
            Assert.That(fen, Is.EqualTo(fens[ply]));
            Assert.That(game.GetFenAtPly(ply), Is.SameAs(fen));
        }

        Assert.That(game.ToFen(), Is.SameAs(game.GetFenAtPly(game.PlyCount)));
        var snapshot = game.History;
        Assert.That(snapshot.Count, Is.EqualTo(moves.Count));
        for (int i = 0; i < moves.Count; i++)
        {
            Assert.That(snapshot[i], Is.EqualTo(new MoveHistoryEntry(moves[i], fens[i], fens[i + 1], results[i])));
        }
    }

    private static void AssertSpanAtPly(ChessGame game, int ply, string expected)
    {
        char[] buffer = Enumerable.Repeat('?', expected.Length + 4).ToArray();
        Assert.That(game.TryFormatFenAtPly(ply, buffer.AsSpan(1, expected.Length - 1), out int written), Is.False);
        Assert.That(written, Is.Zero);
        Assert.That(buffer, Is.All.EqualTo('?'));
        Assert.That(game.TryFormatFenAtPly(ply, buffer.AsSpan(1, expected.Length), out written), Is.True);
        Assert.That(written, Is.EqualTo(expected.Length));
        Assert.That(new string(buffer, 1, written), Is.EqualTo(expected));
        Assert.That(buffer[0], Is.EqualTo('?'));
        Assert.That(buffer[(written + 1)..], Is.All.EqualTo('?'));
    }

    private static CoordinateMove[] ParseLine(string line)
        => line.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(text => CoordinateMove.Parse(text)).ToArray();

    private static void Play(ChessGame game, string line)
    {
        foreach (var move in ParseLine(line))
        {
            Assert.That(game.MakeMove(move), Is.Not.EqualTo(MoveResult.None));
        }
    }
}
