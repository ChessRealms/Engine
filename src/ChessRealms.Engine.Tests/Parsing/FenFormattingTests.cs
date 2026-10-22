using ChessRealms.Engine.Parsing;
using System.Globalization;
using System.Numerics;

namespace ChessRealms.Engine.Tests.Parsing;

internal class FenFormattingTests
{
    private static IEnumerable<string> CanonicalFens()
    {
        yield return ChessGame.StartingFen;
        yield return "r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1";
        yield return "7k/8/8/3pP3/8/8/8/7K w - d6 0 1";
        yield return "7k/8/8/8/3Pp3/8/8/7K b - d3 0 1";
        yield return "7k/8/8/p7/8/8/8/7K w - a6 0 1";
        yield return "7k/8/8/8/7P/8/8/7K b - h3 0 1";
        yield return "k3r3/8/8/3pP3/8/8/8/4K3 w - d6 0 1";
        yield return "k7/1Q6/2K5/8/8/8/8/8 b - - 0 1";
        yield return "k7/8/1QK5/8/8/8/8/8 b - - 0 1";
        yield return "7k/8/8/8/8/8/8/7K w - - 0 1";
        yield return "7k/8/8/8/8/8/8/7K b - - 2147483648 9223372036854775808";

        for (int flags = 0; flags < 16; flags++)
        {
            string rights = new("KQkq".Where((_, bit) => (flags & (1 << bit)) != 0).ToArray());
            yield return $"r3k2r/8/8/8/8/8/8/R3K2R w {(rights.Length == 0 ? "-" : rights)} - 17 42";
        }
    }

    [TestCaseSource(nameof(CanonicalFens))]
    public void FormatsCanonicalFenIntoExactAndLargerSpans(string fen)
    {
        var game = ChessGame.FromFen(fen);
        AssertFormatting(game, fen);
        Assert.That(FenFormatter.FormatWithoutCounters(game.Position), Is.EqualTo(string.Join(' ', fen.Split(' ').Take(4))));
    }

    [TestCaseSource(nameof(CanonicalFens))]
    public void EveryShortDestinationRemainsUnchanged(string fen)
    {
        var game = ChessGame.FromFen(fen);
        char[] buffer = Enumerable.Repeat('?', fen.Length + 2).ToArray();
        for (int capacity = 0; capacity < fen.Length; capacity++)
        {
            Assert.That(game.TryFormatFen(buffer.AsSpan(1, capacity), out int written), Is.False);
            Assert.That(written, Is.Zero);
            Assert.That(buffer, Is.All.EqualTo('?'));
        }
    }

    [TestCase(1)]
    [TestCase(9)]
    [TestCase(19)]
    [TestCase(20)]
    [TestCase(21)]
    [TestCase(22)]
    [TestCase(38)]
    [TestCase(128)]
    [TestCase(4096)]
    public void ArbitraryPrecisionCountersAroundPowersOfTenAreNeverTruncated(int exponent)
    {
        BigInteger power = BigInteger.Pow(10, exponent);
        foreach (int offset in new[] { -1, 0, 1 })
        {
            string halfmove = (power + offset).ToString(CultureInfo.InvariantCulture);
            string fullmove = (power - offset).ToString(CultureInfo.InvariantCulture);
            string fen = $"7k/8/8/8/8/8/8/7K b - - {halfmove} {fullmove}";
            var game = ChessGame.FromFen(fen);
            AssertFormatting(game, fen);

            char[] buffer = Enumerable.Repeat('?', fen.Length + 2).ToArray();
            Assert.That(game.TryFormatFen(buffer.AsSpan(1, fen.Length - 1), out int written), Is.False);
            Assert.That(written, Is.Zero);
            Assert.That(buffer, Is.All.EqualTo('?'));
            Assert.That(game.TryFormatFen(Span<char>.Empty, out written), Is.False);
            Assert.That(written, Is.Zero);
        }
    }

    [Test]
    [SetCulture("ar-SA")]
    public void FormattingIsInvariantAndNormalizesLeadingZeroCounters()
    {
        var game = ChessGame.FromFen("7k/8/8/8/8/8/8/7K b - - 00042 000123456789012345678901234567890");
        AssertFormatting(game, "7k/8/8/8/8/8/8/7K b - - 42 123456789012345678901234567890");
    }

    [Test]
    public void FormatterWithoutValidationSupportsTheMaximumLengthWithoutCounters()
    {
        // Internal geometric fixtures may occupy all squares and omit kings.
        string fields = $"{string.Join('/', Enumerable.Repeat("pppppppp", 8))} w KQkq h6";
        string fen = fields + " 0 1";
        Assert.That(FenParser.TryParseSyntaxOnly(fen, out var position), Is.True);
        Assert.That(FenFormatter.FormatWithoutValidation(position), Is.EqualTo(fen));
        Assert.That(FenFormatter.FormatWithoutCounters(position), Is.EqualTo(fields));
        Span<char> buffer = stackalloc char[fen.Length];
        Assert.That(FenFormatter.TryFormatWithoutValidation(position, buffer, out int written), Is.True);
        Assert.That(new string(buffer[..written]), Is.EqualTo(fen));
    }

    [Test]
    public void FormattingTracksMovesAndUndoWithoutChangingHistoryOrDrawClaims()
    {
        var game = new ChessGame();
        foreach (string text in new[] { "g1f3", "g8f6", "f3g1", "f6g8", "g1f3", "g8f6", "f3g1", "f6g8" })
        {
            Assert.That(game.MakeMove(CoordinateMove.Parse(text)), Is.Not.EqualTo(MoveResult.None));
            AssertFormatting(game, game.ToFen());
        }

        Assert.That(game.ClaimDraw(DrawClaim.ThreefoldRepetition), Is.True);
        var before = game.Clone();
        AssertFormatting(game, game.ToFen());
        Assert.Multiple(() =>
        {
            Assert.That(game.Position, Is.EqualTo(before.Position));
            Assert.That(game.History, Is.EqualTo(before.History));
            Assert.That(game.Outcome, Is.EqualTo(before.Outcome));
            Assert.That(game.DrawIntendedMove, Is.EqualTo(before.DrawIntendedMove));
            Assert.That(game.RepetitionCount, Is.EqualTo(before.RepetitionCount));
        });

        while (game.UndoMove())
        {
            AssertFormatting(game, game.ToFen());
        }

        AssertFormatting(game, ChessGame.StartingFen);
        AssertFormatting(before, before.ToFen());
    }

    private static void AssertFormatting(ChessGame game, string expected)
    {
        Assert.That(FenFormatter.Format(game.Position), Is.EqualTo(expected));
        Assert.That(game.ToFen(), Is.EqualTo(expected));
        char[] buffer = Enumerable.Repeat('?', expected.Length + 6).ToArray();
        foreach (int capacity in new[] { expected.Length, expected.Length + 2 })
        {
            Assert.That(game.TryFormatFen(buffer.AsSpan(2, capacity), out int written), Is.True);
            Assert.That(written, Is.EqualTo(expected.Length));
            Assert.That(new string(buffer, 2, written), Is.EqualTo(expected));
            Assert.That(buffer[..2], Is.All.EqualTo('?'));
            Assert.That(buffer[(written + 2)..], Is.All.EqualTo('?'));
        }
    }
}
