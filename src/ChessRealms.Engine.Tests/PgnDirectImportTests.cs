namespace ChessRealms.Engine.Tests;

internal class PgnDirectImportTests
{
    [TestCase("*")]
    [TestCase("1. e4 e5 2. Nf3 Nc6 *")]
    [TestCase("1. f3 e5 2. g4 Qh4# 0-1")]
    [TestCase("[SetUp \"1\"][FEN \"r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1\"] 1. O-O *")]
    [TestCase("[SetUp \"1\"][FEN \"7k/8/8/3pP3/8/8/8/7K w - d6 0 1\"] 1. exd6 *")]
    [TestCase("[SetUp \"1\"][FEN \"7k/P7/8/8/8/8/8/7K w - - 0 1\"] 1. a8=N 1/2-1/2")]
    [TestCase("[SetUp \"1\"][FEN \"1r5k/P7/8/8/8/8/8/7K w - - 0 1\"] 1. axb8=Q+ *")]
    [TestCase("[SetUp \"1\"][FEN \"7k/8/8/8/8/Q1p5/8/Q1Q4K w - - 0 1\"] 1. Qa1xc3+ *")]
    [TestCase("[SetUp \"1\"][FEN \"7k/8/8/8/8/8/8/R6K w - - 149 80\"] 80. Ra8+ 1/2-1/2")]
    [TestCase("[SetUp \"1\"][FEN \"7k/8/8/8/8/8/8/7K w - - 0 1\"] 1/2-1/2")]
    [TestCase("[SetUp \"1\"][FEN \"k7/8/1QK5/8/8/8/8/8 b - - 0 1\"] 1/2-1/2")]
    public void ParseWithGame_MatchesSeparateReplayIncludingUndo(string text)
    {
        var expectedDocument = PgnGame.Parse(text);
        var expected = expectedDocument.ToGame();
        var document = PgnGame.Parse(text, out var game);
        Assert.That(document.ToPgn(), Is.EqualTo(expectedDocument.ToPgn()));
        AssertSameLineThroughUndo(game, expected);
    }

    [Test]
    public void TryParseWithGame_AcceptsSpanSlicesAndReturnsIndependentDocumentAndGames()
    {
        const string text = "[Event \"Київ ♟\"][Custom \"value\"] 1. e4 e5 1-0";
        string input = "prefix" + text + "suffix";
        Assert.That(PgnGame.TryParse(input.AsSpan(6, text.Length), out var document, out var game, out var error), Is.True);
        Assert.That(error, Is.Null);
        Assert.That(game, Is.Not.Null);
        Assert.That(document!.Tags["Event"], Is.EqualTo("Київ ♟"));
        Assert.That(document.Tags["Custom"], Is.EqualTo("value"));
        string originalPgn = document.ToPgn();
        string originalFen = game!.ToFen();
        var replay = document.ToGame();
        var secondDocument = PgnGame.Parse(text, out var secondGame);

        Assert.That(game.UndoMove(), Is.True);
        Assert.That(game.MakeSanMove("c5"), Is.Not.EqualTo(MoveResult.None));
        Assert.That(document.ToPgn(), Is.EqualTo(originalPgn));
        Assert.That(secondDocument.ToPgn(), Is.EqualTo(originalPgn));
        Assert.That(replay.ToFen(), Is.EqualTo(originalFen));
        Assert.That(secondGame.ToFen(), Is.EqualTo(originalFen));
        Assert.That(replay.MakeSanMove("Nf3"), Is.Not.EqualTo(MoveResult.None));
        Assert.That(secondGame.PlyCount, Is.EqualTo(2));
        Assert.That(document.Moves.Count, Is.EqualTo(2));
        Assert.Throws<NotSupportedException>(() => ((IList<CoordinateMove>)document.Moves)[0] = default);
        Assert.Throws<NotSupportedException>(() => ((IDictionary<string, string>)document.Tags)["Event"] = "Changed");
    }

    [TestCase("*", PgnResult.Unknown)]
    [TestCase("1-0", PgnResult.WhiteWin)]
    [TestCase("0-1", PgnResult.BlackWin)]
    [TestCase("1/2-1/2", PgnResult.Draw)]
    public void DeclaredResult_DoesNotInventAnEngineEnding(string marker, PgnResult result)
    {
        Assert.That(PgnGame.TryParse($"[Result \"{marker}\"] 1. e4 {marker}",
            out var document, out var game, out var error), Is.True);
        Assert.That(error, Is.Null);
        Assert.That(document!.Result, Is.EqualTo(result));
        Assert.That(document.Tags["Result"], Is.EqualTo(marker));
        Assert.That(game!.Outcome, Is.EqualTo(GameOutcome.Ongoing));
        Assert.That(game.DrawIntendedMove, Is.Null);
        Assert.That(game.MakeSanMove("e5"), Is.Not.EqualTo(MoveResult.None));
        Assert.That(document.Moves.Count, Is.EqualTo(1));
    }

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(4)]
    public void RepetitionAndClaims_AreRebuiltAndIndependentOfTheDocument(int cycles)
    {
        string text = string.Join(' ', Enumerable.Repeat("Nf3 Nf6 Ng1 Ng8", cycles))
            + (cycles == 4 ? " 1/2-1/2" : " *");
        var document = PgnGame.Parse(text, out var game);
        Assert.That(game.RepetitionCount, Is.EqualTo(cycles + 1));
        if (cycles == 2)
        {
            Assert.That(game.AvailableDrawClaims, Is.EqualTo(DrawClaim.ThreefoldRepetition));
            var claimGame = game.Clone();
            Assert.That(claimGame.ClaimDraw(DrawClaim.ThreefoldRepetition), Is.True);
            Assert.That(game.IsFinished, Is.False);
            Assert.That(document.Result, Is.EqualTo(PgnResult.Unknown));
        }
        else if (cycles == 4)
        {
            Assert.That(game.Outcome.Reason, Is.EqualTo(FinishReason.FivefoldRepetition));
        }

        AssertSameLineThroughUndo(game, document.ToGame());
    }

    [Test]
    [SetCulture("ar-SA")]
    public void CustomBlackStart_PreservesLargeCountersAndInitialFen()
    {
        string number = new('9', 80);
        string fen = $"7k/8/8/8/8/8/8/R6K b - - 17 {number}";
        string text = $"[SetUp \"1\"][FEN \"{fen}\"] {number}... Kg8 1{new string('0', 80)}. Ra2 *";
        var document = PgnGame.Parse(text, out var game);
        Assert.That(document.InitialFen, Is.EqualTo(fen));
        Assert.That(game.FullmoveNumber, Is.EqualTo(System.Numerics.BigInteger.Pow(10, 80)));
        Assert.That(game.HalfmoveClock, Is.EqualTo(new System.Numerics.BigInteger(19)));
        AssertSameLineThroughUndo(game, document.ToGame());
        Assert.That(game.ToFen(), Is.EqualTo(fen));
    }

    [TestCase(17)]
    [TestCase(12345)]
    public void FullGames_PreserveStateThroughUndoAndContinuation(int seed)
    {
        var expected = new ChessGame();
        var random = new Random(seed);
        for (int ply = 0; ply < 120 && !expected.IsFinished; ++ply)
        {
            var moves = expected.GetLegalMoves();
            expected.MakeMove(moves[random.Next(moves.Count)]);
        }

        string text = PgnGame.FromGame(expected).ToPgn();
        var document = PgnGame.Parse(text, out var game);
        Assert.That(document.ToPgn(), Is.EqualTo(text));
        AssertSameLineThroughUndo(game, expected);
    }

    private static void AssertSameLineThroughUndo(ChessGame actual, ChessGame expected)
    {
        Assert.That(actual, Is.Not.SameAs(expected));
        Assert.That(actual.History, Is.EqualTo(expected.History));
        while (true)
        {
            AssertSameState(actual, expected);
            bool undone = actual.UndoMove();
            Assert.That(undone, Is.EqualTo(expected.UndoMove()));
            if (!undone)
            {
                break;
            }
        }

        if (!expected.IsFinished)
        {
            var move = expected.GetLegalMoves()[0];
            Assert.That(actual.MakeMove(move), Is.EqualTo(expected.MakeMove(move)));
            AssertSameState(actual, expected);
            Assert.That(actual.UndoMove(), Is.True);
            Assert.That(expected.UndoMove(), Is.True);
        }
    }

    private static void AssertSameState(ChessGame actual, ChessGame expected)
    {
        Assert.Multiple(() =>
        {
            Assert.That(actual.Position, Is.EqualTo(expected.Position));
            Assert.That(actual.ToFen(), Is.EqualTo(expected.ToFen()));
            Assert.That(actual.Outcome, Is.EqualTo(expected.Outcome));
            Assert.That(actual.PlyCount, Is.EqualTo(expected.PlyCount));
            Assert.That(actual.RepetitionCount, Is.EqualTo(expected.RepetitionCount));
            Assert.That(actual.AvailableDrawClaims, Is.EqualTo(expected.AvailableDrawClaims));
            Assert.That(actual.DrawIntendedMove, Is.EqualTo(expected.DrawIntendedMove));
            Assert.That(actual.GetLegalMoves(), Is.EqualTo(expected.GetLegalMoves()));
        });
    }
}
