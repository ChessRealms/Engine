namespace ChessRealms.Engine.Tests;

internal class PgnImportTests
{
    [TestCase("e4 e5 Nf3 Nc6 *")]
    [TestCase("1.e4 e5 2.Nf3 Nc6 *")]
    [TestCase("1. e4 1... e5 2. Nf3 2... Nc6 *")]
    [TestCase("1 e4 1 e5 2 Nf3 2 Nc6 *")]
    [TestCase("\uFEFF \t\r\n1 . . e4\n1. . . e5\r2. Nf3\tNc6 *\v")]
    [TestCase("[Event\"Example\"][Result\"*\"]1.e4 e5 2.Nf3 Nc6*")]
    public void ImportLayoutVariants_ResolveSameMoves(string text)
    {
        var document = PgnGame.Parse(text);
        Assert.That(document.Moves, Is.EqualTo(new[] { "e2e4", "e7e5", "g1f3", "b8c6" }.Select(move => CoordinateMove.Parse(move))));
        Assert.That(document.Result, Is.EqualTo(PgnResult.Unknown));
        Assert.That(document.Tags.Count, Is.EqualTo(7));
    }

    [Test]
    public void SpanSlice_UsesOnlySuppliedInput()
    {
        ReadOnlySpan<char> input = "prefix1. e4 *suffix";
        Assert.That(PgnGame.TryParse(input.Slice(6, 7), out var document, out var diagnostic), Is.True);
        Assert.That(diagnostic, Is.Null);
        Assert.That(document!.Moves.Count, Is.EqualTo(1));
    }

    [TestCase("", PgnErrorCode.MissingResult, 0, 0)]
    [TestCase("1. e4", PgnErrorCode.MissingResult, 5, 1)]
    [TestCase("2. e4 *", PgnErrorCode.InvalidMoveNumber, 0, 0)]
    [TestCase("1. *", PgnErrorCode.InvalidMoveNumber, 0, 0)]
    [TestCase("1. 1. e4 *", PgnErrorCode.InvalidMoveNumber, 3, 0)]
    [TestCase("1. e5 *", PgnErrorCode.InvalidSan, 3, 0)]
    [TestCase("1. e4 e4 *", PgnErrorCode.InvalidSan, 6, 1)]
    [TestCase("1. e4+ *", PgnErrorCode.InvalidSan, 3, 0)]
    [TestCase("1. Ng1f3 *", PgnErrorCode.InvalidSan, 3, 0)]
    [TestCase("1. e4 \0 *", PgnErrorCode.UnexpectedToken, 6, 1)]
    [TestCase("1. e4 \f *", PgnErrorCode.UnexpectedToken, 6, 1)]
    [TestCase("1. e4 \u00a0 *", PgnErrorCode.UnexpectedToken, 6, 1)]
    [TestCase("1. e4 ! *", PgnErrorCode.UnsupportedAnnotation, 6, 1)]
    [TestCase("1. e4!? *", PgnErrorCode.UnsupportedAnnotation, 5, 0)]
    [TestCase("1. e4 {comment} *", PgnErrorCode.UnsupportedAnnotation, 6, 1)]
    [TestCase("1. e4 ;comment\n*", PgnErrorCode.UnsupportedAnnotation, 6, 1)]
    [TestCase("1. e4 $1 *", PgnErrorCode.UnsupportedAnnotation, 6, 1)]
    [TestCase("1. e4 (1. d4) *", PgnErrorCode.UnsupportedAnnotation, 6, 1)]
    [TestCase("1. e4 ) *", PgnErrorCode.UnsupportedAnnotation, 6, 1)]
    [TestCase("%escape\n*", PgnErrorCode.UnsupportedConstruct, 0, 0)]
    [TestCase("<reserved> *", PgnErrorCode.UnsupportedConstruct, 0, 0)]
    [TestCase("* {comment}", PgnErrorCode.UnsupportedAnnotation, 2, 0)]
    [TestCase("* [Event \"second\"] *", PgnErrorCode.MultipleGames, 2, 0)]
    [TestCase("* 1. e4 *", PgnErrorCode.UnexpectedToken, 2, 0)]
    [TestCase("* *", PgnErrorCode.UnexpectedToken, 2, 0)]
    [TestCase("1. f3 e5 2. g4 Qh4# *", PgnErrorCode.ResultMismatch, 20, 4)]
    [TestCase("1. f3 e5 2. g4 Qh4# 1-0", PgnErrorCode.ResultMismatch, 20, 4)]
    [TestCase("1. f3 e5 2. g4 Qh4# 3. e4 0-1", PgnErrorCode.MoveAfterGameEnd, 23, 4)]
    public void Failures_AreAtomicAndHaveStructuredLocations(string text, PgnErrorCode code, int offset, int ply)
    {
        Assert.That(PgnGame.TryParse(text, out var document, out var error), Is.False);
        Assert.That(document, Is.Null);
        Assert.That(error, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(error!.Code, Is.EqualTo(code));
            Assert.That(error.Offset, Is.EqualTo(offset));
            Assert.That(error.PlyIndex, Is.EqualTo(ply));
            Assert.That(error.Message, Is.Not.Empty);
        });
        var exception = Assert.Throws<PgnParseException>(() => PgnGame.Parse(text));
        Assert.That(exception!.Diagnostic, Is.EqualTo(error));
        Assert.That(exception, Is.InstanceOf<FormatException>());
        AssertDirectFailure(text, error!);
    }

    [TestCase("[Event \"a\"][Event \"b\"] *", PgnErrorCode.DuplicateTag)]
    [TestCase("[_Event \"a\"] *", PgnErrorCode.InvalidTag)]
    [TestCase("[Event bare] *", PgnErrorCode.InvalidTag)]
    [TestCase("[Event \"a\\n\"] *", PgnErrorCode.InvalidTag)]
    [TestCase("[Event \"a\nb\"] *", PgnErrorCode.InvalidTag)]
    [TestCase("[Event \"a\"", PgnErrorCode.InvalidTag)]
    [TestCase("[Event \"a", PgnErrorCode.InvalidTag)]
    [TestCase("[Event \"a\" extra] *", PgnErrorCode.InvalidTag)]
    [TestCase("[Result \"win\"] *", PgnErrorCode.InvalidResult)]
    [TestCase("[Variant \"Chess960\"] *", PgnErrorCode.UnsupportedVariant)]
    [TestCase("[SetUp \"1\"] *", PgnErrorCode.InvalidSetup)]
    [TestCase("[SetUp \"2\"] *", PgnErrorCode.InvalidSetup)]
    [TestCase("[FEN \"invalid\"] *", PgnErrorCode.InvalidSetup)]
    [TestCase("[SetUp \"0\"][FEN \"invalid\"] *", PgnErrorCode.InvalidSetup)]
    [TestCase("[SetUp \"1\"][FEN \"invalid\"] *", PgnErrorCode.InvalidFen)]
    public void HeaderFailures_DoNotHavePlyIndex(string text, PgnErrorCode code)
    {
        Assert.That(PgnGame.TryParse(text, out var document, out var error), Is.False);
        Assert.That(document, Is.Null);
        Assert.That(error!.Code, Is.EqualTo(code));
        Assert.That(error.PlyIndex, Is.Null);
        Assert.That(error.Offset, Is.InRange(0, text.Length));
        AssertDirectFailure(text, error);
    }

    [TestCase("\r\n")]
    [TestCase("\r")]
    [TestCase("\n")]
    public void Diagnostic_CountsUtf16OffsetsAndOneBasedLinesAndColumns(string newline)
    {
        string text = $"[Event \"Київ ♟\"]{newline}{newline}1. e4{newline}\te4 *";
        Assert.That(PgnGame.TryParse(text, out _, out var error), Is.False);
        Assert.That(error!.Offset, Is.EqualTo(text.LastIndexOf("e4", StringComparison.Ordinal)));
        Assert.That(error.Line, Is.EqualTo(4));
        Assert.That(error.Column, Is.EqualTo(2));
        Assert.That(error.PlyIndex, Is.EqualTo(1));
        AssertDirectFailure(text, error);
    }

    [Test]
    public void ResultTagAndMovetext_MustAgree()
    {
        Assert.That(PgnGame.TryParse("[Result \"1-0\"] 1. e4 0-1", out _, out var error), Is.False);
        Assert.That(error!.Code, Is.EqualTo(PgnErrorCode.ResultMismatch));
        Assert.That(error.Offset, Is.EqualTo(21));
        Assert.That(error.PlyIndex, Is.EqualTo(1));
        AssertDirectFailure("[Result \"1-0\"] 1. e4 0-1", error);
        Assert.That(PgnGame.Parse("[Result \"1-0\"] 1. e4 1-0").Result, Is.EqualTo(PgnResult.WhiteWin));
    }

    [Test]
    public void TerminalStartingFen_RequiresItsResult()
    {
        const string header = "[SetUp \"1\"][FEN \"7k/8/8/8/8/8/8/7K w - - 0 1\"] ";
        Assert.That(PgnGame.TryParse(header + "*", out _, out var error), Is.False);
        Assert.That(error!.Code, Is.EqualTo(PgnErrorCode.ResultMismatch));
        Assert.That(PgnGame.Parse(header + "1/2-1/2").ToGame().IsFinished, Is.True);
        Assert.That(PgnGame.TryParse(header + "1. Kg1 1/2-1/2", out _, out error), Is.False);
        Assert.That(error!.Code, Is.EqualTo(PgnErrorCode.MoveAfterGameEnd));
    }

    [Test]
    public void TagAndTokenLimits_AreEnforcedWithoutTruncation()
    {
        string max = new('a', 255);
        var valid = PgnGame.Parse($"[{max} \"{max}\"] *");
        Assert.That(valid.Tags[max], Is.EqualTo(max));
        string[] invalid = [$"[{max}a \"v\"] *", $"[Name \"{max}a\"] *", "[Name \"\ud800\"] *"];
        foreach (string text in invalid)
        {
            Assert.That(PgnGame.TryParse(text, out _, out var error), Is.False);
            Assert.That(error!.Code, Is.EqualTo(PgnErrorCode.InvalidTag));
            AssertDirectFailure(text, error);
        }

        Assert.That(PgnGame.TryParse(new string('a', 256) + " *", out _, out var tokenError), Is.False);
        Assert.That(tokenError!.Code, Is.EqualTo(PgnErrorCode.UnexpectedToken));
        AssertDirectFailure(new string('a', 256) + " *", tokenError);
    }

    [Test]
    public void ArbitraryMalformedInput_ReturnsDiagnosticInsteadOfLeakingParserExceptions()
    {
        var random = new Random(2048);
        const string alphabet = "[]\"\\{}();!?$*0123456789.e4x+#_ \t\r\n\uFEFF\ud800";
        for (int sample = 0; sample < 500; ++sample)
        {
            string text = new(Enumerable.Range(0, random.Next(1, 80))
                .Select(_ => alphabet[random.Next(alphabet.Length)]).ToArray());
            bool success = PgnGame.TryParse(text, out var document, out var error);
            Assert.That(document is not null, Is.EqualTo(success), text);
            Assert.That(error is null, Is.EqualTo(success), text);
            bool directSuccess = PgnGame.TryParse(text, out var directDocument, out var game, out var directError);
            Assert.That(directSuccess, Is.EqualTo(success), text);
            Assert.That(directDocument is not null, Is.EqualTo(success), text);
            Assert.That(game is not null, Is.EqualTo(success), text);
            Assert.That(directError, Is.EqualTo(error), text);
        }
    }

    private static void AssertDirectFailure(string text, PgnDiagnostic expected)
    {
        ChessGame? game = new();
        PgnGame? document = PgnGame.FromGame(game);
        Assert.That(PgnGame.TryParse(text, out document, out game, out var diagnostic), Is.False);
        Assert.That(document, Is.Null);
        Assert.That(game, Is.Null);
        Assert.That(diagnostic, Is.EqualTo(expected));
        var exception = Assert.Throws<PgnParseException>(() => PgnGame.Parse(text, out _));
        Assert.That(exception!.Diagnostic, Is.EqualTo(expected));
    }
}
