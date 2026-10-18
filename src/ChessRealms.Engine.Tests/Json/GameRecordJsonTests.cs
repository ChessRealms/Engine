using System.Text.Json;
using System.Text.Json.Nodes;

namespace ChessRealms.Engine.Tests.Json;

internal class GameRecordJsonTests
{
    private const string RepetitionCycle = "g1f3 g8f6 f3g1 f6g8";
    private const string RookEnding = "4k3/8/8/8/8/8/8/R3K3 w - - ";

    // A fixed version-one fixture must remain readable independently of the current serializer.
    private const string VersionOneRecord = """
        {
          "schemaVersion": 1,
          "initialFen": "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1",
          "moves": ["e2e4", "e7e5"],
          "drawClaim": null
        }
        """;

    [Test]
    public void VersionOne_ImportsFixedFixtureAndExportsExpectedFields()
    {
        var record = GameRecord.Deserialize(VersionOneRecord);
        using var document = JsonDocument.Parse(GameRecord.Serialize(record));
        var root = document.RootElement;

        Assert.Multiple(() =>
        {
            Assert.That(record.InitialFen, Is.EqualTo(ChessGame.StartingFen));
            Assert.That(record.Moves.Select(move => move.ToString()), Is.EqualTo(new[] { "e2e4", "e7e5" }));
            Assert.That(record.DrawClaim, Is.Null);
            Assert.That(root.EnumerateObject().Select(property => property.Name),
                Is.EquivalentTo(new[] { "schemaVersion", "initialFen", "moves", "drawClaim" }));
            Assert.That(root.GetProperty("schemaVersion").GetInt32(), Is.EqualTo(1));
            Assert.That(root.GetProperty("initialFen").GetString(), Is.EqualTo(ChessGame.StartingFen));
            Assert.That(root.GetProperty("moves").EnumerateArray().Select(move => move.GetString()),
                Is.EqualTo(new[] { "e2e4", "e7e5" }));
            Assert.That(root.GetProperty("drawClaim").ValueKind, Is.EqualTo(JsonValueKind.Null));
        });

        var moves = (IList<CoordinateMove>)record.Moves;
        Assert.Throws<NotSupportedException>(() => moves.Clear());
    }

    [TestCase(ChessGame.StartingFen, "")]
    [TestCase(ChessGame.StartingFen, "e2e4 e7e5 g1f3 b8c6 f1b5 a7a6 b5c6")]
    [TestCase(ChessGame.StartingFen, "e2e4 a7a6 e4e5 d7d5 e5d6")]
    [TestCase("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1", "e1g1 e8c8")]
    [TestCase("7k/P7/8/8/8/8/8/7K w - - 0 1", "a7a8q")]
    [TestCase("7k/P7/8/8/8/8/8/7K w - - 0 1", "a7a8r")]
    [TestCase("7k/P7/8/8/8/8/8/7K w - - 0 1", "a7a8b")]
    [TestCase("7k/P7/8/8/8/8/8/7K w - - 0 1", "a7a8n")]
    [TestCase(RookEnding + "7 999999999999999999999", "a1a2")]
    [TestCase(ChessGame.StartingFen, "f2f3 e7e5 g2g4 d8h4")]
    [TestCase(ChessGame.StartingFen, RepetitionCycle + " " + RepetitionCycle)]
    [TestCase(ChessGame.StartingFen,
        RepetitionCycle + " " + RepetitionCycle + " " + RepetitionCycle + " " + RepetitionCycle)]
    [TestCase(RookEnding + "149 80", "a1a2")]
    public void JsonRoundTrip_PreservesGameHistoryOutcomeAndUndo(string fen, string moves)
    {
        var original = ChessGame.FromFen(fen);
        Play(original, moves);

        var record = GameRecord.Deserialize(GameRecord.Serialize(original.ExportRecord()));
        var restored = ChessGame.FromRecord(record);

        Assert.That(record.InitialFen, Is.EqualTo(fen));
        AssertEquivalent(original, restored);
        while (original.UndoMove())
        {
            Assert.That(restored.UndoMove(), Is.True);
            AssertEquivalent(original, restored);
        }

        Assert.That(restored.UndoMove(), Is.False);
    }

    [TestCase(ChessGame.StartingFen, RepetitionCycle + " " + RepetitionCycle,
        DrawClaim.ThreefoldRepetition, "threefoldRepetition", null)]
    [TestCase(ChessGame.StartingFen, RepetitionCycle + " g1f3 g8f6 f3g1",
        DrawClaim.ThreefoldRepetition, "threefoldRepetition", "f6g8")]
    [TestCase(RookEnding + "100 50", "", DrawClaim.FiftyMoveRule, "fiftyMoveRule", null)]
    [TestCase(RookEnding + "99 50", "", DrawClaim.FiftyMoveRule, "fiftyMoveRule", "a1a2")]
    public void JsonRoundTrip_PreservesClaimWithoutPlayingIntendedMove(
        string fen, string moves, DrawClaim reason, string reasonText, string? intendedMoveText)
    {
        var original = ChessGame.FromFen(fen);
        Play(original, moves);
        CoordinateMove? intendedMove = intendedMoveText is null ? null : CoordinateMove.Parse(intendedMoveText);
        Assert.That(original.ClaimDraw(reason, intendedMove), Is.True);

        string json = GameRecord.Serialize(original.ExportRecord());
        using var document = JsonDocument.Parse(json);
        var claimJson = document.RootElement.GetProperty("drawClaim");
        Assert.That(claimJson.GetProperty("reason").GetString(), Is.EqualTo(reasonText));
        Assert.That(claimJson.GetProperty("intendedMove").GetString(), Is.EqualTo(intendedMoveText));

        var record = GameRecord.Deserialize(json);
        Assert.That(record.DrawClaim, Is.EqualTo(new DrawClaimRecord(reason, intendedMove)));
        AssertEquivalent(original, ChessGame.FromRecord(record));
    }

    [Test]
    public void Deserialize_AllowsReorderedFieldsAndOmittedOptionalClaim()
    {
        string json = $$"""
            { "moves": [], "initialFen": "{{ChessGame.StartingFen}}", "schemaVersion": 1 }
            """;

        var record = GameRecord.Deserialize(json);

        Assert.That(record.Moves, Is.Empty);
        Assert.That(record.DrawClaim, Is.Null);
    }

    [Test]
    public void Deserialize_AllowsClaimWithoutOptionalIntendedMove()
    {
        const string json = """
            {
              "schemaVersion": 1,
              "initialFen": "4k3/8/8/8/8/8/8/R3K3 w - - 100 50",
              "moves": [],
              "drawClaim": { "reason": "fiftyMoveRule" }
            }
            """;

        var record = GameRecord.Deserialize(json);

        Assert.That(record.DrawClaim, Is.EqualTo(new DrawClaimRecord(DrawClaim.FiftyMoveRule)));
    }

    [TestCase("")]
    [TestCase("{")]
    [TestCase("null")]
    [TestCase("[]")]
    [TestCase("42")]
    [TestCase("{}")]
    [TestCase(VersionOneRecord + " {}")]
    public void Deserialize_RejectsInvalidRoot(string json)
    {
        Assert.Throws<JsonException>(() => GameRecord.Deserialize(json));
    }

    [TestCase("schemaVersion")]
    [TestCase("initialFen")]
    [TestCase("moves")]
    public void Deserialize_RejectsMissingRequiredFields(string field)
    {
        var document = JsonNode.Parse(VersionOneRecord)!.AsObject();
        document.Remove(field);

        Assert.Throws<JsonException>(() => GameRecord.Deserialize(document.ToJsonString()));
    }

    [TestCase("schemaVersion", "null")]
    [TestCase("schemaVersion", "0")]
    [TestCase("schemaVersion", "-1")]
    [TestCase("schemaVersion", "2")]
    [TestCase("schemaVersion", "2147483648")]
    [TestCase("schemaVersion", "1.5")]
    [TestCase("schemaVersion", "\"1\"")]
    [TestCase("initialFen", "null")]
    [TestCase("initialFen", "42")]
    [TestCase("initialFen", "\"not a FEN\"")]
    [TestCase("moves", "null")]
    [TestCase("moves", "\"e2e4\"")]
    [TestCase("moves", "[null]")]
    [TestCase("moves", "[42]")]
    [TestCase("moves", "[{}]")]
    [TestCase("moves", "[\"e2e2\"]")]
    [TestCase("moves", "[\"e2e5\"]")]
    [TestCase("drawClaim", "[]")]
    [TestCase("drawClaim", "\"fiftyMoveRule\"")]
    [TestCase("drawClaim", "{}")]
    [TestCase("drawClaim", "{\"reason\":null}")]
    [TestCase("drawClaim", "{\"reason\":2}")]
    [TestCase("drawClaim", "{\"reason\":\"unknown\"}")]
    [TestCase("drawClaim", "{\"reason\":\"none\"}")]
    [TestCase("drawClaim", "{\"reason\":\"threefoldRepetition, fiftyMoveRule\"}")]
    [TestCase("drawClaim", "{\"reason\":\"threefoldRepetition\"}")]
    [TestCase("drawClaim", "{\"reason\":\"fiftyMoveRule\",\"intendedMove\":\"a1b2\"}")]
    [TestCase("drawClaim", "{\"reason\":\"fiftyMoveRule\",\"intendedMove\":{}}")]
    [TestCase("extra", "true")]
    [TestCase("Moves", "[]")]
    public void Deserialize_RejectsInvalidFields(string field, string value)
    {
        var document = JsonNode.Parse(VersionOneRecord)!;
        document[field] = JsonNode.Parse(value);

        Assert.Throws<JsonException>(() => GameRecord.Deserialize(document.ToJsonString()));
    }

    [TestCase("\"reason\":\"fiftyMoveRule\",\"extra\":true")]
    [TestCase("\"reason\":\"fiftyMoveRule\",\"reason\":\"fiftyMoveRule\"")]
    [TestCase("\"reason\":\"fiftyMoveRule\",\"intendedMove\":null,\"intendedMove\":null")]
    public void Deserialize_RejectsUnknownOrDuplicateClaimFields(string claimFields)
    {
        string json = $$"""
            {
              "schemaVersion": 1,
              "initialFen": "4k3/8/8/8/8/8/8/R3K3 w - - 100 50",
              "moves": [],
              "drawClaim": { {{claimFields}} }
            }
            """;

        Assert.Throws<JsonException>(() => GameRecord.Deserialize(json));
    }

    [TestCase("schemaVersion", "1")]
    [TestCase("schemaVersion", "2")]
    [TestCase("schema\\u0056ersion", "1")]
    [TestCase("moves", "[]")]
    [TestCase("drawClaim", "null")]
    public void Deserialize_RejectsDuplicateRootFields(string field, string value)
    {
        string json = VersionOneRecord.Insert(1, $"\"{field}\":{value},");

        Assert.Throws<JsonException>(() => GameRecord.Deserialize(json));
    }

    [TestCase("[\"e2e4\",\"d2d4\"]", 1)]
    [TestCase("[\"f2f3\",\"e7e5\",\"g2g4\",\"d8h4\",\"a2a3\"]", 4)]
    public void Deserialize_ReportsUnplayableMoveIndex(string moves, int index)
    {
        var document = JsonNode.Parse(VersionOneRecord)!;
        document["moves"] = JsonNode.Parse(moves);

        var error = Assert.Throws<JsonException>(() => GameRecord.Deserialize(document.ToJsonString()));

        Assert.That(error!.Message, Does.Contain($"index {index}"));
        Assert.That(error.InnerException, Is.TypeOf<FormatException>());
    }

    [Test]
    public void Methods_RejectNullArguments()
    {
        Assert.Throws<ArgumentNullException>(() => GameRecord.Serialize(null!));
        Assert.Throws<ArgumentNullException>(() => GameRecord.Deserialize(null!));
    }

    private static void Play(ChessGame game, string moves)
    {
        foreach (string move in moves.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            Assert.That(game.MakeMove(CoordinateMove.Parse(move)), Is.Not.EqualTo(MoveResult.None), move);
        }
    }

    private static void AssertEquivalent(ChessGame expected, ChessGame actual)
    {
        Assert.Multiple(() =>
        {
            Assert.That(actual.ToFen(), Is.EqualTo(expected.ToFen()));
            Assert.That(actual.Outcome, Is.EqualTo(expected.Outcome));
            Assert.That(actual.History, Is.EqualTo(expected.History));
            Assert.That(actual.RepetitionCount, Is.EqualTo(expected.RepetitionCount));
            Assert.That(actual.AvailableDrawClaims, Is.EqualTo(expected.AvailableDrawClaims));
            Assert.That(actual.DrawIntendedMove, Is.EqualTo(expected.DrawIntendedMove));
            Assert.That(actual.GetLegalMoves(), Is.EquivalentTo(expected.GetLegalMoves()));
        });
    }
}
