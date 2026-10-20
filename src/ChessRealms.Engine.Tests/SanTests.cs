namespace ChessRealms.Engine.Tests;

internal class SanTests
{
    private const string ThreeKnights = "7k/8/8/8/8/1N6/8/1N3N1K w - - 0 1";
    private const string PinnedKnight = "7k/8/8/8/1b6/2N5/8/4K1N1 w - - 0 1";
    private const string Castles = "r3k2r/8/8/8/8/8/8/R3K2R";
    private const string Promotion = "7k/P7/8/8/8/8/8/7K w - - 0 1";
    private const string Mate = "7k/5Q2/6K1/8/8/8/8/8 w - - 149 80";

    private static readonly string[][] CanonicalCases =
    [
        [ChessGame.StartingFen, "e2e4", "e4"],
        [ChessGame.StartingFen, "g1f3", "Nf3"],
        ["7k/8/8/3p4/4P3/8/8/7K w - - 0 1", "e4d5", "exd5"],
        ["7k/8/8/3p4/4P3/8/8/7K b - - 0 1", "d5e4", "dxe4"],
        ["7k/8/8/3p4/8/2N5/8/7K w - - 0 1", "c3d5", "Nxd5"],
        ["7k/p7/8/8/8/8/8/2B4K w - - 0 1", "c1e3", "Be3"],
        ["7k/8/8/8/8/8/8/R6K w - - 0 1", "h1g2", "Kg2"],
        ["7k/8/8/8/8/8/8/R6K w - - 0 1", "a1a8", "Ra8+"],
        [Mate, "f7g7", "Qg7#"],
        [Mate, "f7e6", "Qe6"],
        [Castles + " w KQkq - 0 1", "e1g1", "O-O"],
        [Castles + " w KQkq - 0 1", "e1c1", "O-O-O"],
        [Castles + " b KQkq - 0 1", "e8g8", "O-O"],
        [Castles + " b KQkq - 0 1", "e8c8", "O-O-O"],
        ["5k2/8/8/8/8/8/8/4K2R w K - 0 1", "e1g1", "O-O+"],
        ["3k4/8/8/8/8/8/8/R3K3 w Q - 0 1", "e1c1", "O-O-O+"],
        ["7k/8/8/3pP3/8/8/8/7K w - d6 0 1", "e5d6", "exd6"],
        ["7k/8/8/8/3Pp3/8/8/7K b - d3 0 1", "e4d3", "exd3"],
        ["8/8/8/R2pP2k/8/8/8/7K w - d6 0 1", "e5d6", "exd6+"],
        ["4k3/p7/8/8/8/8/4B3/4R2K w - - 0 1", "e2b5", "Bb5+"],
        [PinnedKnight, "g1e2", "Ne2"],
        ["7k/8/8/8/8/8/8/1N3N1K w - - 0 1", "b1d2", "Nbd2"],
        ["7k/8/8/8/8/8/8/1N3N1K w - - 0 1", "f1d2", "Nfd2"],
        ["7k/8/8/8/8/5N2/8/1N5K w - - 0 1", "b1d2", "Nbd2"],
        ["7k/8/8/8/8/1N6/8/1N5K w - - 0 1", "b1d2", "N1d2"],
        ["7k/8/8/8/8/1N6/8/1N5K w - - 0 1", "b3d2", "N3d2"],
        [ThreeKnights, "b1d2", "Nb1d2"],
        [ThreeKnights, "b3d2", "N3d2"],
        [ThreeKnights, "f1d2", "Nfd2"],
        ["7k/8/8/8/8/Q1p5/8/Q1Q4K w - - 0 1", "a1c3", "Qa1xc3+"],
        [Promotion, "a7a8q", "a8=Q+"],
        [Promotion, "a7a8r", "a8=R+"],
        [Promotion, "a7a8b", "a8=B"],
        [Promotion, "a7a8n", "a8=N"],
        ["1r5k/P7/8/8/8/8/8/7K w - - 0 1", "a7b8q", "axb8=Q+"],
        ["7k/8/8/8/8/8/p7/1R5K b - - 0 1", "a2b1r", "axb1=R+"],
        ["7k/8/8/8/8/8/p7/7K b - - 0 1", "a2a1n", "a1=N"],
        ["8/P1k5/8/8/8/8/8/7K w - - 0 1", "a7a8n", "a8=N+"],
        ["4k3/8/8/8/8/2N1N3/8/4R2K w - - 0 1", "e3d5", "Ned5+"],
        ["4k3/8/8/8/8/2N1N3/8/4R2K w - - 0 1", "c3d5", "Ncd5"],
        ["7k/8/8/8/8/8/8/R6K w - - 149 80", "a1a8", "Ra8+"],
        ["7k/8/8/8/8/8/8/R6K b - - 0 999999999999999999999999", "h8g8", "Kg8"]
    ];

    [TestCaseSource(nameof(CanonicalCases))]
    public void CanonicalNotation_FormatsParsesAndAppliesWithoutChangingQueries(string fen, string coordinate, string san)
    {
        var game = ChessGame.FromFen(fen);
        var before = game.Clone();
        var move = CoordinateMove.Parse(coordinate);
        Assert.That(game.FormatSan(move), Is.EqualTo(san));

        Span<char> buffer = stackalloc char[9];
        buffer.Fill('!');
        Assert.That(game.TryFormatSan(move, buffer[..san.Length], out int written), Is.True);
        Assert.That(written, Is.EqualTo(san.Length));
        Assert.That(buffer[..written].ToString(), Is.EqualTo(san));
        Assert.That(buffer[written..].ToString(), Is.EqualTo(new string('!', 9 - written)));
        Assert.That(game.ParseSan(san), Is.EqualTo(move));
        Assert.That(game.TryParseSan(san, out var parsed), Is.True);
        Assert.That(parsed, Is.EqualTo(move));
        AssertEquivalent(before, game);

        MoveResult expected = before.MakeMove(move);
        Assert.That(expected, Is.Not.EqualTo(MoveResult.None));
        Assert.That(game.MakeSanMove(san), Is.EqualTo(expected));
        AssertEquivalent(before, game);
        Assert.That(game.UndoMove(), Is.True);
        Assert.That(before.UndoMove(), Is.True);
        AssertEquivalent(before, game);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    [TestCase("e4 ")]
    [TestCase(" e4")]
    [TestCase("e4\n")]
    [TestCase("E4")]
    [TestCase("nf3")]
    [TestCase("Pe4")]
    [TestCase("e2e4")]
    [TestCase("Ng1f3")]
    [TestCase("Ngf3")]
    [TestCase("N1f3")]
    [TestCase("Nxf3")]
    [TestCase("Nf3+")]
    [TestCase("e4#")]
    [TestCase("e4!")]
    [TestCase("e4?!")]
    [TestCase("e4$1")]
    [TestCase("e4 e5")]
    [TestCase("1.e4")]
    [TestCase("e5")]
    [TestCase("e4=Q")]
    [TestCase("Nf3=Q")]
    [TestCase("0-0")]
    [TestCase("O-O")]
    [TestCase("O-O-O")]
    [TestCase("♘f3")]
    [TestCase("e４")]
    [TestCase("e4\0")]
    [TestCase("--")]
    [TestCase("*")]
    public void InvalidStartingNotation_IsRejectedWithoutMutation(string? text)
    {
        AssertRejected(new ChessGame(), text);
    }

    [TestCase(ThreeKnights, "Nd2")]
    [TestCase(ThreeKnights, "Nbd2")]
    [TestCase(ThreeKnights, "N1d2")]
    [TestCase(ThreeKnights, "Nb3d2")]
    [TestCase(PinnedKnight, "Nge2")]
    [TestCase(PinnedKnight, "Nce2")]
    [TestCase(Promotion, "a8")]
    [TestCase(Promotion, "a8Q+")]
    [TestCase(Promotion, "a8=q+")]
    [TestCase(Promotion, "a8=P")]
    [TestCase(Promotion, "a8=K")]
    [TestCase(Promotion, "a8=Q")]
    [TestCase(Promotion, "a8=Q#")]
    [TestCase(Promotion, "a8=Q++")]
    [TestCase(Mate, "Qg7")]
    [TestCase(Mate, "Qg7+")]
    [TestCase(Mate, "Qe6=")]
    [TestCase("7k/8/8/3pP3/8/8/8/7K w - d6 0 1", "ed6")]
    [TestCase("7k/8/8/3pP3/8/8/8/7K w - d6 0 1", "exd6 e.p.")]
    [TestCase("7k/8/8/r4pPK/8/8/8/8 w - f6 0 1", "gxf6")]
    [TestCase("4kr2/8/8/8/8/8/8/R3K2R w KQ - 0 1", "O-O")]
    // Both knights can play d5, but only e3d5 uncovers the rook's check.
    [TestCase("4k3/8/8/8/8/2N1N3/8/4R2K w - - 0 1", "Nd5+")]
    public void NoncanonicalAmbiguousOrIllegalNotation_IsRejected(string fen, string text)
    {
        AssertRejected(ChessGame.FromFen(fen), text);
    }

    [TestCaseSource(nameof(CanonicalCases))]
    public void ShortDestinations_AreNeverPartiallyWritten(string fen, string coordinate, string san)
    {
        var game = ChessGame.FromFen(fen);
        var before = game.Clone();
        Span<char> buffer = stackalloc char[7];
        for (int length = 0; length < san.Length; ++length)
        {
            buffer.Fill('!');
            Assert.That(game.TryFormatSan(CoordinateMove.Parse(coordinate), buffer[..length], out int written), Is.False);
            Assert.That(written, Is.Zero);
            Assert.That(buffer.ToString(), Is.EqualTo("!!!!!!!"));
        }

        AssertEquivalent(before, game);
    }

    [Test]
    public void InvalidCoordinateMoves_CannotBeFormatted()
    {
        var game = new ChessGame();
        var before = game.Clone();
        CoordinateMove[] moves = [default, CoordinateMove.Parse("e2e5"), CoordinateMove.Parse("e2e4q"),
            CoordinateMove.Parse("e7e5"), CoordinateMove.Parse("e1g1")];
        Span<char> buffer = stackalloc char[7];
        foreach (var move in moves)
        {
            buffer.Fill('!');
            Assert.That(game.TryFormatSan(move, buffer, out int written), Is.False);
            Assert.That(written, Is.Zero);
            Assert.That(buffer.ToString(), Is.EqualTo("!!!!!!!"));
            Assert.Throws<ArgumentException>(() => game.FormatSan(move));
        }

        AssertEquivalent(before, game);
    }

    [TestCase("7k/6Q1/6K1/8/8/8/8/8 b - - 0 1", "h8g8", "Kg8")]
    [TestCase("7k/5Q2/6K1/8/8/8/8/8 b - - 0 1", "h8g8", "Kg8")]
    [TestCase("7k/8/8/8/8/8/8/7K w - - 0 1", "h1g1", "Kg1")]
    [TestCase("7k/8/8/8/8/8/8/R6K w - - 150 1", "a1a2", "Ra2")]
    public void FinishedPositions_RejectAllSanOperations(string fen, string coordinate, string san)
    {
        var game = ChessGame.FromFen(fen);
        Assert.That(game.IsFinished, Is.True);
        AssertFinished(game, coordinate, san);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Queries_PreserveRepetitionClaimsHistoryAndEveryUndo(bool claim)
    {
        var game = new ChessGame();
        Play(game, "g1f3 g8f6 f3g1 f6g8 g1f3 g8f6 f3g1");
        if (claim)
        {
            Assert.That(game.ClaimDraw(DrawClaim.ThreefoldRepetition, CoordinateMove.Parse("f6g8")), Is.True);
            AssertFinished(game, "f6g8", "Ng8");
        }
        else
        {
            var move = CoordinateMove.Parse("f6g8");
            var before = game.Clone();
            Assert.That(game.ParseSan("Ng8"), Is.EqualTo(move));
            Assert.That(game.FormatSan(move), Is.EqualTo("Ng8"));
            AssertRejected(game, "Ng8+");
            AssertEquivalent(before, game);
            AssertUndoEquivalent(before, game);
        }
    }

    [Test]
    public void FivefoldRepetition_RejectsFurtherSanMoves()
    {
        var game = new ChessGame();
        for (int i = 0; i < 4; ++i)
        {
            Play(game, "g1f3 g8f6 f3g1 f6g8");
        }

        Assert.That(game.Outcome.Reason, Is.EqualTo(FinishReason.FivefoldRepetition));
        AssertFinished(game, "g1f3", "Nf3");
    }

    [Test]
    public void ClaimWithoutHistory_RemainsIntactAfterQueriesAndFailedUndo()
    {
        var game = ChessGame.FromFen("4k3/8/8/8/8/8/8/R3K3 w - - 99 50");
        Assert.That(game.ClaimDraw(DrawClaim.FiftyMoveRule, CoordinateMove.Parse("a1a2")), Is.True);
        AssertFinished(game, "a1a2", "Ra2");
    }

    [Test]
    public void SpanInput_UsesOnlyTheSuppliedSlice()
    {
        var game = new ChessGame();
        ReadOnlySpan<char> input = "prefixNf3suffix";
        Assert.That(game.ParseSan(input.Slice(6, 3)), Is.EqualTo(CoordinateMove.Parse("g1f3")));
        Assert.That(game.History, Is.Empty);
    }

    [TestCase(17)]
    [TestCase(12345)]
    public void DeterministicGames_RoundTripEveryLegalMoveAndPreserveState(int seed)
    {
        var game = new ChessGame();
        var random = new Random(seed);
        for (int ply = 0; ply < 100 && !game.IsFinished; ++ply)
        {
            var before = game.Clone();
            var moves = game.GetLegalMoves();
            var notation = new HashSet<string>(StringComparer.Ordinal);
            foreach (var move in moves)
            {
                string san = game.FormatSan(move);
                Assert.That(notation.Add(san), Is.True, san);
                Assert.That(game.ParseSan(san), Is.EqualTo(move), san);
                var copy = game.Clone();
                var result = copy.MakeMove(move);
                Assert.That(san.EndsWith('#'), Is.EqualTo(result.HasFlag(MoveResult.Checkmate)));
                Assert.That(san.EndsWith('+'), Is.EqualTo(result.HasFlag(MoveResult.Check)
                    && !result.HasFlag(MoveResult.Checkmate)));
            }

            AssertEquivalent(before, game);
            var chosen = moves[random.Next(moves.Count)];
            string text = game.FormatSan(chosen);
            Assert.That(game.MakeSanMove(text), Is.EqualTo(before.MakeMove(chosen)));
            AssertEquivalent(before, game);
        }
    }

    [TestCaseSource(nameof(CanonicalCases))]
    public void SpanQueries_DoNotAllocateAfterWarmup(string fen, string coordinate, string san)
    {
        var game = ChessGame.FromFen(fen);
        var move = CoordinateMove.Parse(coordinate);
        Span<char> buffer = stackalloc char[7];
        for (int i = 0; i < 64; ++i)
        {
            game.TryFormatSan(move, buffer, out _);
            game.TryParseSan(san, out _);
        }

        long start = GC.GetAllocatedBytesForCurrentThread();
        bool success = true;
        for (int i = 0; i < 128; ++i)
        {
            success &= game.TryFormatSan(move, buffer, out _);
            success &= game.TryParseSan(san, out var parsed) && parsed == move;
            success &= !game.TryParseSan("invalid", out _);
        }

        long allocated = GC.GetAllocatedBytesForCurrentThread() - start;
        Assert.That(success, Is.True);
        Assert.That(allocated, Is.Zero);
    }

    private static void AssertRejected(ChessGame game, string? text)
    {
        var before = game.Clone();
        Assert.That(game.TryParseSan(text, out var move), Is.False);
        Assert.That(move, Is.EqualTo(default(CoordinateMove)));
        Assert.Throws<FormatException>(() => game.ParseSan(text));
        Assert.That(game.MakeSanMove(text), Is.EqualTo(MoveResult.None));
        AssertEquivalent(before, game);
    }

    private static void AssertFinished(ChessGame game, string coordinate, string san)
    {
        var before = game.Clone();
        var move = CoordinateMove.Parse(coordinate);
        Span<char> buffer = stackalloc char[7];
        buffer.Fill('!');
        Assert.That(game.TryFormatSan(move, buffer, out int written), Is.False);
        Assert.That(written, Is.Zero);
        Assert.That(buffer.ToString(), Is.EqualTo("!!!!!!!"));
        Assert.Throws<ArgumentException>(() => game.FormatSan(move));
        AssertRejected(game, san);
        AssertEquivalent(before, game);
        AssertUndoEquivalent(before, game);
    }

    private static void AssertEquivalent(ChessGame expected, ChessGame actual)
    {
        Assert.Multiple(() =>
        {
            Assert.That(actual.Position, Is.EqualTo(expected.Position));
            Assert.That(actual.ToFen(), Is.EqualTo(expected.ToFen()));
            Assert.That(actual.Outcome, Is.EqualTo(expected.Outcome));
            Assert.That(actual.State, Is.EqualTo(expected.State));
            Assert.That(actual.History, Is.EqualTo(expected.History));
            Assert.That(actual.RepetitionCount, Is.EqualTo(expected.RepetitionCount));
            Assert.That(actual.AvailableDrawClaims, Is.EqualTo(expected.AvailableDrawClaims));
            Assert.That(actual.DrawIntendedMove, Is.EqualTo(expected.DrawIntendedMove));
            Assert.That(actual.GetLegalMoves(), Is.EqualTo(expected.GetLegalMoves()));
            Assert.That(GameRecord.Serialize(actual.ExportRecord()), Is.EqualTo(GameRecord.Serialize(expected.ExportRecord())));
        });
    }

    private static void AssertUndoEquivalent(ChessGame expected, ChessGame actual)
    {
        while (expected.UndoMove())
        {
            Assert.That(actual.UndoMove(), Is.True);
            AssertEquivalent(expected, actual);
        }

        Assert.That(actual.UndoMove(), Is.False);
        AssertEquivalent(expected, actual);
    }

    private static void Play(ChessGame game, string moves)
    {
        foreach (string move in moves.Split(' '))
        {
            Assert.That(game.MakeMove(CoordinateMove.Parse(move)), Is.Not.EqualTo(MoveResult.None));
        }
    }
}
