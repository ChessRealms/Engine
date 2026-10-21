namespace ChessRealms.Engine.Tests;

internal class PgnModelTests
{
    [Test]
    public void Snapshot_OwnsTagsAndCurrentLine_AndReplaysIndependentGames()
    {
        var game = new ChessGame();
        game.MakeSanMove("e4");
        game.MakeSanMove("e5");
        game.UndoMove();
        var tags = new Dictionary<string, string> { ["Event"] = "Example", ["Custom"] = "value" };
        var document = PgnGame.FromGame(game, tags, PgnResult.WhiteWin);
        tags["Event"] = "Changed";
        game.MakeSanMove("c5");
        var replay = document.ToGame();
        var secondReplay = document.ToGame();

        Assert.Multiple(() =>
        {
            Assert.That(document.InitialFen, Is.EqualTo(ChessGame.StartingFen));
            Assert.That(document.Moves, Is.EqualTo(new[] { CoordinateMove.Parse("e2e4") }));
            Assert.That(document.Tags["Event"], Is.EqualTo("Example"));
            Assert.That(document.Tags["Date"], Is.EqualTo("????.??.??"));
            Assert.That(document.Tags["Result"], Is.EqualTo("1-0"));
            Assert.That(document.Result, Is.EqualTo(PgnResult.WhiteWin));
            Assert.That(replay.Outcome.Result, Is.EqualTo(GameResult.Ongoing));
        });
        Assert.Throws<NotSupportedException>(() => ((IList<CoordinateMove>)document.Moves)[0] = default);
        Assert.Throws<NotSupportedException>(() => ((IDictionary<string, string>)document.Tags)["Event"] = "Changed");
        Assert.That(replay.UndoMove(), Is.True);
        Assert.That(secondReplay.History.Count, Is.EqualTo(1));
        Assert.That(game.History.Count, Is.EqualTo(2));
        Assert.That(PgnGame.Parse(document.ToPgn()).ToPgn(), Is.EqualTo(document.ToPgn()));
    }

    [TestCase("*", PgnResult.Unknown)]
    [TestCase("1-0", PgnResult.WhiteWin)]
    [TestCase("0-1", PgnResult.BlackWin)]
    [TestCase("1/2-1/2", PgnResult.Draw)]
    public void UnfinishedPosition_AllowsDeclaredResults(string marker, PgnResult result)
    {
        var game = new ChessGame();
        var document = PgnGame.FromGame(game, new Dictionary<string, string> { ["Result"] = marker });
        Assert.That(document.Result, Is.EqualTo(result));
        Assert.That(document.ToPgn(), Does.EndWith($"\n\n{marker}\n\n"));
        Assert.That(document.ToGame().IsFinished, Is.False);
        Assert.That(game.IsFinished, Is.False);
    }

    [TestCase("bad name", "text")]
    [TestCase("_name", "text")]
    [TestCase("", "text")]
    [TestCase("Name", "line\nbreak")]
    [TestCase("Name", "tab\t")]
    [TestCase("Name", "\u007f")]
    [TestCase("Name", "\u2028")]
    [TestCase("Result", "draw")]
    [TestCase("Variant", "Chess960")]
    [TestCase("SetUp", "1")]
    [TestCase("SetUp", "2")]
    [TestCase("FEN", ChessGame.StartingFen)]
    public void InvalidTags_AreRejected(string name, string value)
    {
        Assert.Throws<ArgumentException>(() =>
            PgnGame.FromGame(new ChessGame(), new Dictionary<string, string> { [name] = value }));
    }

    [Test]
    public void DuplicateNullOversizedAndMalformedUnicodeTags_AreRejected()
    {
        KeyValuePair<string, string>[] duplicate = [new("Event", "a"), new("Event", "b")];
        Assert.Throws<ArgumentException>(() => PgnGame.FromGame(new ChessGame(), duplicate));
        KeyValuePair<string, string>[] nullName = [new(null!, "a")];
        KeyValuePair<string, string>[] nullValue = [new("Name", null!)];
        Assert.Throws<ArgumentException>(() => PgnGame.FromGame(new ChessGame(), nullName));
        Assert.Throws<ArgumentException>(() => PgnGame.FromGame(new ChessGame(), nullValue));
        foreach (var pair in new[] { new KeyValuePair<string, string>(new string('a', 256), "v"),
            new("Name", new string('a', 256)), new("Name", "\ud800") })
        {
            Assert.Throws<ArgumentException>(() => PgnGame.FromGame(new ChessGame(), new[] { pair }));
        }
    }

    [Test]
    public void InvalidArgumentsAndConflictingResults_AreRejected()
    {
        Assert.Throws<ArgumentNullException>(() => PgnGame.FromGame(null!));
        Assert.Throws<ArgumentNullException>(() => PgnGame.FromGame(new ChessGame()).WriteTo(null!));
        Assert.Throws<ArgumentNullException>(() => new PgnParseException(null!));
        Assert.Throws<ArgumentException>(() => PgnGame.FromGame(new ChessGame(), result: (PgnResult)99));
        Assert.Throws<ArgumentException>(() => PgnGame.FromGame(new ChessGame(),
            new Dictionary<string, string> { ["Result"] = "1-0" }, PgnResult.BlackWin));
        var mate = PgnGame.Parse("1. f3 e5 2. g4 Qh4# 0-1").ToGame();
        Assert.That(PgnGame.FromGame(mate).Result, Is.EqualTo(PgnResult.BlackWin));
        Assert.Throws<ArgumentException>(() => PgnGame.FromGame(mate, result: PgnResult.Unknown));
        Assert.Throws<ArgumentException>(() => PgnGame.FromGame(mate, result: PgnResult.WhiteWin));
    }

    [Test]
    public void SetupTags_MustMatchInitialPosition_AndNormalizeRedundantSetup()
    {
        const string fen = "7k/8/8/8/8/8/8/R6K b - - 0 23";
        var custom = ChessGame.FromFen(fen);
        custom.MakeSanMove("Kg8");
        var document = PgnGame.FromGame(custom);
        Assert.That(document.InitialFen, Is.EqualTo(fen));
        Assert.That(document.Tags["FEN"], Is.EqualTo(fen));
        Assert.That(document.Tags["SetUp"], Is.EqualTo("1"));
        var tags = new Dictionary<string, string> { ["SetUp"] = "1", ["FEN"] = ChessGame.StartingFen };
        Assert.Throws<ArgumentException>(() => PgnGame.FromGame(custom, tags));
        Assert.That(PgnGame.FromGame(new ChessGame(), tags).Tags.ContainsKey("FEN"), Is.False);
        tags["FEN"] = "invalid";
        Assert.Throws<ArgumentException>(() => PgnGame.FromGame(custom, tags));
        Assert.Throws<ArgumentException>(() =>
            PgnGame.FromGame(custom, new Dictionary<string, string> { ["SetUp"] = "0" }));
        Assert.That(PgnGame.FromGame(new ChessGame(),
            new Dictionary<string, string> { ["SetUp"] = "0" }).Tags.ContainsKey("SetUp"), Is.False);
    }

    [Test]
    public void DrawClaim_ResultSurvives_ButReasonAndUnplayedIntendedMoveAreNotInvented()
    {
        var game = ChessGame.FromFen("4k3/8/8/8/8/8/8/R3K3 w - - 99 50");
        Assert.That(game.ClaimDraw(DrawClaim.FiftyMoveRule, CoordinateMove.Parse("a1a2")), Is.True);
        var document = PgnGame.FromGame(game);
        var imported = PgnGame.Parse(document.ToPgn());
        Assert.That(imported.Result, Is.EqualTo(PgnResult.Draw));
        Assert.That(imported.Moves, Is.Empty);
        Assert.That(imported.ToGame().Outcome.Result, Is.EqualTo(GameResult.Ongoing));
        Assert.That(game.DrawIntendedMove, Is.EqualTo(CoordinateMove.Parse("a1a2")));
        Assert.That(game.Outcome.Reason, Is.EqualTo(FinishReason.FiftyMoveRule));
        Assert.Throws<ArgumentException>(() => PgnGame.FromGame(game, result: PgnResult.WhiteWin));
    }
}
