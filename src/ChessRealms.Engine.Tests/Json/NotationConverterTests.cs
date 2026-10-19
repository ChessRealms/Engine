using System.Buffers;
using System.Text;
using System.Text.Json;

namespace ChessRealms.Engine.Tests.Json;

internal class NotationConverterTests
{
    [TestCase("e2e4")]
    [TestCase("a7a8q")]
    [TestCase("a7a8r")]
    [TestCase("a7a8b")]
    [TestCase("a7a8n")]
    public void Move_SerializesAsStringWithoutConfiguringOptions(string text)
    {
        var move = CoordinateMove.Parse(text);
        string json = JsonSerializer.Serialize(move);

        Assert.That(json, Is.EqualTo($"\"{text}\""));
        Assert.That(JsonSerializer.Deserialize<CoordinateMove>(json), Is.EqualTo(move));
    }

    [TestCase("a1")]
    [TestCase("e4")]
    [TestCase("h8")]
    public void Square_SerializesAsStringWithoutConfiguringOptions(string text)
    {
        var square = Square.Parse(text);
        string json = JsonSerializer.Serialize(square);

        Assert.That(json, Is.EqualTo($"\"{text}\""));
        Assert.That(JsonSerializer.Deserialize<Square>(json), Is.EqualTo(square));
    }

    [TestCase("null")]
    [TestCase("42")]
    [TestCase("true")]
    [TestCase("{}")]
    [TestCase("[]")]
    [TestCase("\"\"")]
    [TestCase("\"e2e\"")]
    [TestCase("\"e2e2\"")]
    [TestCase("\"e2e4qq\"")]
    [TestCase("\"a7a8Q\"")]
    [TestCase("\"\\u04352e4\"")]
    [TestCase("\"e2e4\\u0000\"")]
    [TestCase("\"e2\\uD800\"")]
    [TestCase("\"e2\\uDC00\"")]
    public void Move_RejectsInvalidJson(string json)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<CoordinateMove>(json));
    }

    [TestCase("null")]
    [TestCase("42")]
    [TestCase("true")]
    [TestCase("{}")]
    [TestCase("[]")]
    [TestCase("\"\"")]
    [TestCase("\"A1\"")]
    [TestCase("\"a0\"")]
    [TestCase("\"i1\"")]
    [TestCase("\"a1 \"")]
    [TestCase("\"\\u04301\"")]
    [TestCase("\"\\uD800\"")]
    [TestCase("\"\\uDC00\"")]
    public void Square_RejectsInvalidJson(string json)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Square>(json));
    }

    [Test]
    public void Writers_RejectDefaultValues()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Serialize(default(CoordinateMove)));
        Assert.Throws<JsonException>(() => JsonSerializer.Serialize(default(Square)));
    }

    [Test]
    public void Readers_RejectMalformedUtf8AsJsonErrors()
    {
        byte[] squareJson = [(byte)'"', (byte)'a', 0xFF, (byte)'"'];
        byte[] moveJson = [(byte)'"', (byte)'e', (byte)'2', (byte)'e', 0xFF, (byte)'"'];

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Square>(squareJson));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<CoordinateMove>(moveJson));
    }

    [TestCase("\"e2e4\"", "e2e4")]
    [TestCase("\"\\u0065\\u0032\\u0065\\u0034\"", "e2e4")]
    [TestCase("\"\\u0061\\u0037\\u0061\\u0038\\u006E\"", "a7a8n")]
    public void Move_ReadsEscapedAndSegmentedStrings(string json, string text)
    {
        AssertReadsAtEverySplit(json, CoordinateMove.Parse(text));
    }

    [TestCase("\"h8\"", "h8")]
    [TestCase("\"\\u0068\\u0038\"", "h8")]
    public void Square_ReadsEscapedAndSegmentedStrings(string json, string text)
    {
        AssertReadsAtEverySplit(json, Square.Parse(text));
    }

    [TestCase(DrawClaim.None, "none")]
    [TestCase(DrawClaim.ThreefoldRepetition, "threefoldRepetition")]
    [TestCase(DrawClaim.FiftyMoveRule, "fiftyMoveRule")]
    [TestCase(DrawClaim.ThreefoldRepetition | DrawClaim.FiftyMoveRule, "threefoldRepetition, fiftyMoveRule")]
    public void DrawClaim_UsesStringsAndPreservesFlagsOutsideGameRecords(DrawClaim claim, string text)
    {
        string json = JsonSerializer.Serialize(claim);

        Assert.That(json, Is.EqualTo($"\"{text}\""));
        Assert.That(JsonSerializer.Deserialize<DrawClaim>(json), Is.EqualTo(claim));
    }

    [TestCase("1")]
    [TestCase("\"1\"")]
    [TestCase("\"unknown\"")]
    [TestCase("null")]
    public void DrawClaim_RejectsNumbersAndUnknownNames(string json)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<DrawClaim>(json));
    }

    [Test]
    public void DrawClaimRecord_ComposesReasonAndIntendedMoveConverters()
    {
        var claim = new DrawClaimRecord(DrawClaim.FiftyMoveRule, CoordinateMove.Parse("a1a2"));
        string json = JsonSerializer.Serialize(claim);
        using var document = JsonDocument.Parse(json);

        Assert.That(document.RootElement.GetProperty("Reason").GetString(), Is.EqualTo("fiftyMoveRule"));
        Assert.That(document.RootElement.GetProperty("IntendedMove").GetString(), Is.EqualTo("a1a2"));
        Assert.That(JsonSerializer.Deserialize<DrawClaimRecord>(json), Is.EqualTo(claim));
    }

    private static void AssertReadsAtEverySplit<T>(string json, T expected)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        Assert.That(JsonSerializer.Deserialize<T>(bytes), Is.EqualTo(expected));

        for (int split = 1; split < bytes.Length; ++split)
        {
            var first = new JsonSegment(bytes.AsMemory(0, split));
            var last = first.Append(bytes.AsMemory(split));
            var sequence = new ReadOnlySequence<byte>(first, 0, last, last.Memory.Length);
            var reader = new Utf8JsonReader(sequence);

            Assert.That(JsonSerializer.Deserialize<T>(ref reader), Is.EqualTo(expected), $"Split at byte {split}");
        }
    }

    private sealed class JsonSegment : ReadOnlySequenceSegment<byte>
    {
        public JsonSegment(ReadOnlyMemory<byte> memory)
        {
            Memory = memory;
        }

        public JsonSegment Append(ReadOnlyMemory<byte> memory)
        {
            var segment = new JsonSegment(memory) { RunningIndex = RunningIndex + Memory.Length };
            Next = segment;
            return segment;
        }
    }
}
