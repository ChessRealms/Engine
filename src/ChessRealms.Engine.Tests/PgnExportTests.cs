using System.Globalization;

namespace ChessRealms.Engine.Tests;

internal class PgnExportTests
{
    [Test]
    public void StartingGame_ExportsCanonicalRosterAndTermination()
    {
        const string expected = "[Event \"?\"]\n[Site \"?\"]\n[Date \"????.??.??\"]\n[Round \"?\"]\n"
            + "[White \"?\"]\n[Black \"?\"]\n[Result \"*\"]\n\n*\n\n";
        Assert.That(PgnGame.FromGame(new ChessGame()).ToPgn(), Is.EqualTo(expected));
    }

    [Test]
    public void Tags_AreEscapedAndSortedOrdinally_AndWriterRemainsOpen()
    {
        var tags = new Dictionary<string, string>
        {
            ["z"] = "last", ["A"] = "first", ["Event"] = "Київ \"Open\" \\ 2026 ♟",
            ["event"] = "case sensitive", ["Variant"] = "Standard"
        };
        var document = PgnGame.FromGame(new ChessGame(), tags);
        using var writer = new StringWriter { NewLine = "\r\n" };
        document.WriteTo(writer);
        string text = writer.ToString();
        Assert.That(text, Is.EqualTo(document.ToPgn()));
        Assert.That(text, Does.StartWith("[Event \"Київ \\\"Open\\\" \\\\ 2026 ♟\"]\n"));
        Assert.That(text, Does.Contain("[A \"first\"]\n[Variant \"Standard\"]\n[event \"case sensitive\"]\n[z \"last\"]"));
        Assert.That(text, Does.Not.Contain("\r"));
        Assert.That(PgnGame.Parse(text).Tags, Is.EquivalentTo(document.Tags));
        writer.Write("still open");
        Assert.That(writer.ToString(), Does.EndWith("still open"));
    }

    [Test]
    public void BlackStartingPosition_UsesFenCountersBeyondInt64_AndInvariantFormatting()
    {
        const string number = "999999999999999999999999";
        var game = ChessGame.FromFen($"7k/8/8/8/8/8/8/R6K b - - 0 {number}");
        game.MakeSanMove("Kg8");
        game.MakeSanMove("Ra2");
        var culture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-SA");
            var document = PgnGame.FromGame(game);
            string text = document.ToPgn();
            Assert.That(text, Does.Contain($"[FEN \"7k/8/8/8/8/8/8/R6K b - - 0 {number}\"]\n[SetUp \"1\"]"));
            Assert.That(text, Does.EndWith($"{number}... Kg8 1000000000000000000000000. Ra2 *\n\n"));
            Assert.That(PgnGame.Parse(text).ToGame().ToFen(), Is.EqualTo(game.ToFen()));
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    }

    [TestCase(29)]
    [TestCase(30)]
    [TestCase(80)]
    public void MoveNumbers_PreserveAllDigitsAcrossFormattingBufferAndLineBoundaries(int digits)
    {
        string number = new('9', digits);
        var game = ChessGame.FromFen($"7k/8/8/8/8/8/8/R6K b - - 0 {number}");
        game.MakeSanMove("Kg8");
        game.MakeSanMove("Ra2");
        var document = PgnGame.FromGame(game);
        string text = document.ToPgn();
        string movetext = text[(text.IndexOf("\n\n", StringComparison.Ordinal) + 2)..];
        Assert.That(movetext.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries),
            Is.EqualTo(new[] { number + "...", "Kg8", "1" + new string('0', digits) + ".", "Ra2", "*" }));
        Assert.That(PgnGame.Parse(text).ToGame().ToFen(), Is.EqualTo(game.ToFen()));
        Assert.That(document.ToPgn(), Is.EqualTo(text));
    }

    [TestCase("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1", "O-O", "*")]
    [TestCase("7k/8/8/3pP3/8/8/8/7K w - d6 0 1", "exd6", "*")]
    [TestCase("7k/P7/8/8/8/8/8/7K w - - 0 1", "a8=N", "1/2-1/2")]
    [TestCase("1r5k/P7/8/8/8/8/8/7K w - - 0 1", "axb8=Q+", "*")]
    [TestCase("7k/8/8/8/8/Q1p5/8/Q1Q4K w - - 0 1", "Qa1xc3+", "*")]
    [TestCase("7k/5Q2/6K1/8/8/8/8/8 w - - 149 80", "Qg7#", "1-0")]
    [TestCase("7k/8/8/8/8/8/8/R6K w - - 149 80", "Ra8+", "1/2-1/2")]
    public void SpecialMovesAndEndings_RoundTrip(string fen, string san, string result)
    {
        var game = ChessGame.FromFen(fen);
        Assert.That(game.MakeSanMove(san), Is.Not.EqualTo(MoveResult.None));
        var document = PgnGame.FromGame(game);
        string text = document.ToPgn();
        Assert.That(text, Does.EndWith($". {san} {result}\n\n"));
        var replay = PgnGame.Parse(text).ToGame();
        Assert.That(replay.ToFen(), Is.EqualTo(game.ToFen()));
        Assert.That(replay.Outcome, Is.EqualTo(game.Outcome));
        Assert.That(replay.History, Is.EqualTo(game.History));
    }

    [TestCase(17)]
    [TestCase(12345)]
    public void FullGames_RoundTripMovesHistoryRepetitionAndUndo_AndWrapMovetext(int seed)
    {
        var game = new ChessGame();
        var random = new Random(seed);
        for (int i = 0; i < 160 && !game.IsFinished; ++i)
        {
            var moves = game.GetLegalMoves();
            game.MakeMove(moves[random.Next(moves.Count)]);
        }

        var document = PgnGame.FromGame(game);
        string text = document.ToPgn();
        string movetext = text[(text.IndexOf("\n\n", StringComparison.Ordinal) + 2)..];
        Assert.That(movetext.Split('\n').All(line => line.Length <= 79 && line == line.Trim()), Is.True);
        Assert.That(movetext.Count(c => c == '\n'), Is.GreaterThan(2));
        var imported = PgnGame.Parse(text);
        Assert.That(imported.ToPgn(), Is.EqualTo(text));
        Assert.That(imported.Moves, Is.EqualTo(document.Moves));
        var replay = imported.ToGame();
        Assert.That(replay.History, Is.EqualTo(game.History));
        do
        {
            Assert.That(replay.ToFen(), Is.EqualTo(game.ToFen()));
            Assert.That(replay.Outcome, Is.EqualTo(game.Outcome));
            Assert.That(replay.RepetitionCount, Is.EqualTo(game.RepetitionCount));
            Assert.That(replay.AvailableDrawClaims, Is.EqualTo(game.AvailableDrawClaims));
            Assert.That(replay.UndoMove(), Is.EqualTo(game.UndoMove()));
        } while (game.History.Count != 0);
    }

    [Test]
    public void FivefoldRepetition_IsPreservedByMainlineReplay()
    {
        string line = string.Join(" ", Enumerable.Repeat("Nf3 Nf6 Ng1 Ng8", 4)) + " 1/2-1/2";
        var document = PgnGame.Parse(line);
        Assert.That(document.ToGame().Outcome.Reason, Is.EqualTo(FinishReason.FivefoldRepetition));
        Assert.That(PgnGame.Parse(document.ToPgn()).ToGame().Outcome.Reason, Is.EqualTo(FinishReason.FivefoldRepetition));
        Assert.That(PgnGame.TryParse(line.Replace("1/2-1/2", "Nf3 1/2-1/2"), out _, out var error), Is.False);
        Assert.That(error!.Code, Is.EqualTo(PgnErrorCode.MoveAfterGameEnd));
    }
}
