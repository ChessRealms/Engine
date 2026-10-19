using System.Text.Json;
using System.Text.Json.Serialization;

namespace ChessRealms.Engine.Json;

internal sealed class CoordinateMoveConverter : JsonConverter<CoordinateMove>
{
    private const int MaxCoordinateMoveLength = 5;

    public override CoordinateMove Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException("Expected a coordinate move string.");
        }

        Span<byte> buffer = stackalloc byte[MaxCoordinateMoveLength];

        int length;
        try
        {
            length = reader.CopyString(buffer);
        }
        catch (ArgumentException ex)
        {
            throw new JsonException("Invalid coordinate move length.", ex);
        }

        if (!CoordinateMove.TryParse(buffer[..length], out var move))
        {
            throw new JsonException("Invalid coordinate move.");
        }

        return move;
    }

    public override void Write(Utf8JsonWriter writer, CoordinateMove value, JsonSerializerOptions options)
    {
        Span<byte> buffer = stackalloc byte[MaxCoordinateMoveLength];
        if (!value.TryFormat(buffer, out int length))
        {
            throw new JsonException("Cannot serialize an invalid coordinate move.");
        }

        writer.WriteStringValue(buffer[..length]);
    }
}
