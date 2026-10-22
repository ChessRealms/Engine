using System.Text.Json;
using System.Text.Json.Serialization;

namespace ChessRealms.Engine.Json;

internal static class GameRecordJsonSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        AllowDuplicateProperties = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true
    };

    internal static string Serialize(GameRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        var document = new GameRecordDocument
        {
            SchemaVersion = GameRecord.SchemaVersion,
            InitialFen = record.InitialFen,
            Moves = record.Moves,
            DrawClaim = record.DrawClaim
        };

        return JsonSerializer.Serialize(document, Options);
    }

    internal static GameRecord Deserialize(string json) => DeserializeValidated(json).Record;

    internal static ChessGame DeserializeGame(string json) => DeserializeValidated(json).Game;

    private static (GameRecord Record, ChessGame Game) DeserializeValidated(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        var document = JsonSerializer.Deserialize<GameRecordDocument>(json, Options);

        if (document.SchemaVersion != GameRecord.SchemaVersion)
        {
            throw new JsonException($"Unsupported game record schema version: {document.SchemaVersion}.");
        }

        var record = new GameRecord(document.InitialFen, document.Moves, document.DrawClaim);
        try
        {
            // Replay is the authority for move legality and draw claims, including intended moves.
            return (record, GameRecord.RestoreGame(record));
        }
        catch (FormatException ex)
        {
            throw new JsonException(ex.Message, ex);
        }
    }

    private readonly record struct GameRecordDocument
    {
        public required int SchemaVersion { get; init; }
        public required string InitialFen { get; init; }
        public required IReadOnlyList<CoordinateMove> Moves { get; init; }
        public DrawClaimRecord? DrawClaim { get; init; }
    }
}
