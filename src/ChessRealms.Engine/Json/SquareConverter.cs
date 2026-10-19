using System.Text.Json;
using System.Text.Json.Serialization;

namespace ChessRealms.Engine.Json;

internal sealed class SquareConverter : JsonConverter<Square>
{
    private const int SquareLength = 2;

    public override Square Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException("Expected a square string.");
        }

        Span<byte> buffer = stackalloc byte[SquareLength];
        int length;
        try
        {
            length = reader.CopyString(buffer);
        }
        catch (ArgumentException ex)
        {
            throw new JsonException("Invalid square length.", ex);
        }

        if (!Square.TryParse(buffer[..length], out var square))
        {
            throw new JsonException("Invalid square.");
        }

        return square;
    }

    public override void Write(Utf8JsonWriter writer, Square value, JsonSerializerOptions options)
    {
        Span<byte> buffer = stackalloc byte[SquareLength];
        if (!value.TryFormat(buffer, out int length))
        {
            throw new JsonException("Cannot serialize an invalid square.");
        }

        writer.WriteStringValue(buffer[..length]);
    }
}
