using System.Globalization;
using System.Text;

namespace ChessRealms.Engine.Tests;

internal class Utf8NotationTests
{
    [Test]
    public void Square_AllSquaresRoundTripThroughUtf8Interfaces()
    {
        for (char rank = '1'; rank <= '8'; ++rank)
        {
            for (char file = 'a'; file <= 'h'; ++file)
            {
                string text = $"{file}{rank}";
                var square = Square.Parse(text);

                AssertRoundTrip(square, text);
                Assert.That(Square.TryParse(Encoding.UTF8.GetBytes(text), out var parsed), Is.True);
                Assert.That(parsed, Is.EqualTo(square));
            }
        }
    }

    [TestCase("e2e4")]
    [TestCase("e1g1")]
    [TestCase("a7a8q")]
    [TestCase("a7a8r")]
    [TestCase("a7a8b")]
    [TestCase("a7a8n")]
    public void Move_RoundTripsThroughUtf8Interfaces(string text)
    {
        var move = CoordinateMove.Parse(text);

        AssertRoundTrip(move, text);
        Assert.That(CoordinateMove.TryParse(Encoding.UTF8.GetBytes(text), out var parsed), Is.True);
        Assert.That(parsed, Is.EqualTo(move));
    }

    [TestCase("")]
    [TestCase("a")]
    [TestCase("a1 ")]
    [TestCase("A1")]
    [TestCase("a0")]
    [TestCase("i1")]
    [TestCase("а1")]
    [TestCase("é")]
    public void Square_RejectsInvalidUtf8Notation(string text)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(text);

        Assert.That(Square.TryParse(bytes, null, out var square), Is.False);
        Assert.That(square, Is.EqualTo(default(Square)));
        Assert.Throws<FormatException>(() => Square.Parse(bytes));
    }

    [TestCase("")]
    [TestCase("e2e")]
    [TestCase("e2e4 ")]
    [TestCase("e2e4qq")]
    [TestCase("E2e4")]
    [TestCase("e2e2")]
    [TestCase("i2e4")]
    [TestCase("e2e9")]
    [TestCase("a7a8Q")]
    [TestCase("a7a8k")]
    [TestCase("a7a8p")]
    [TestCase("е2e4")]
    [TestCase("e2é")]
    public void Move_RejectsInvalidUtf8Notation(string text)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(text);

        Assert.That(CoordinateMove.TryParse(bytes, null, out var move), Is.False);
        Assert.That(move, Is.EqualTo(default(CoordinateMove)));
        Assert.Throws<FormatException>(() => CoordinateMove.Parse(bytes));
    }

    [Test]
    public void Parsers_RejectMalformedUtf8()
    {
        byte[] square = [(byte)'a', 0xFF];
        byte[] move = [(byte)'e', (byte)'2', (byte)'e', 0xFF];

        Assert.That(Square.TryParse(square, out _), Is.False);
        Assert.That(CoordinateMove.TryParse(move, out _), Is.False);
        Assert.Throws<FormatException>(() => Square.Parse(square));
        Assert.Throws<FormatException>(() => CoordinateMove.Parse(move));
    }

    [Test]
    public void Formatters_RejectInvalidDefaultsWithoutWriting()
    {
        AssertFormatFailure(default(Square), 2);
        AssertFormatFailure(default(CoordinateMove), 5);
    }

    [Test]
    public void Formatters_RejectShortBuffersWithoutWriting()
    {
        AssertFormatFailure(Square.Parse("e4"), 0);
        AssertFormatFailure(Square.Parse("e4"), 1);
        AssertFormatFailure(CoordinateMove.Parse("e2e4"), 3);
        AssertFormatFailure(CoordinateMove.Parse("a7a8q"), 4);
    }

    [TestCase("X")]
    [TestCase("GG")]
    public void Formatters_RejectUnsupportedFormats(string format)
    {
        byte[] destination = new byte[5];

        Assert.Throws<FormatException>(() => Square.Parse("e4").TryFormat(destination, out _, format));
        Assert.Throws<FormatException>(() => CoordinateMove.Parse("e2e4").TryFormat(destination, out _, format));
    }

    private static void AssertRoundTrip<T>(T value, string text)
        where T : IUtf8SpanFormattable, IUtf8SpanParsable<T>
    {
        byte[] expected = Encoding.UTF8.GetBytes(text);
        var provider = CultureInfo.GetCultureInfo("tr-TR");

        Assert.That(T.Parse(expected, provider), Is.EqualTo(value));
        Assert.That(T.TryParse(expected, provider, out var parsed), Is.True);
        Assert.That(parsed, Is.EqualTo(value));

        foreach (string format in new[] { "", "G", "g" })
        {
            byte[] buffer = Enumerable.Repeat((byte)0xFF, expected.Length + 2).ToArray();
            Assert.That(value.TryFormat(buffer.AsSpan(1, expected.Length), out int written, format, provider), Is.True);
            Assert.That(written, Is.EqualTo(expected.Length));
            Assert.That(buffer[1..^1], Is.EqualTo(expected));
            Assert.That(buffer[0], Is.EqualTo(0xFF));
            Assert.That(buffer[^1], Is.EqualTo(0xFF));
        }
    }

    private static void AssertFormatFailure<T>(T value, int capacity) where T : IUtf8SpanFormattable
    {
        byte[] buffer = Enumerable.Repeat((byte)0xFF, capacity).ToArray();

        Assert.That(value.TryFormat(buffer, out int written, default, null), Is.False);
        Assert.That(written, Is.Zero);
        Assert.That(buffer, Is.All.EqualTo(0xFF));
    }
}
