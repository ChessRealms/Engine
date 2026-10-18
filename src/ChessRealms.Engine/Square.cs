using ChessRealms.Engine.Json;
using System.Text.Json.Serialization;

namespace ChessRealms.Engine;

/// <summary>A square on a standard chess board.</summary>
[JsonConverter(typeof(SquareConverter))]
public readonly record struct Square : IUtf8SpanFormattable, IUtf8SpanParsable<Square>
{
    private readonly byte encodedIndex;

    internal Square(int index)
    {
        if (index is < 0 or >= 64)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        encodedIndex = (byte)(index + 1);
    }

    /// <summary>Whether this value identifies a board square.</summary>
    public bool IsValid => encodedIndex is >= 1 and <= 64;

    internal int Index => encodedIndex - 1;

    /// <summary>Parses a lowercase square name from <c>a1</c> through <c>h8</c>.</summary>
    public static Square Parse(ReadOnlySpan<char> text)
        => TryParse(text, out var square) ? square : throw new FormatException("Expected a square a1 through h8.");

    /// <summary>Tries to parse a lowercase square name from <c>a1</c> through <c>h8</c>.</summary>
    public static bool TryParse(ReadOnlySpan<char> text, out Square square)
    {
        square = default;
        if (text.Length != 2 || text[0] is < 'a' or > 'h' || text[1] is < '1' or > '8')
        {
            return false;
        }

        square = new((text[1] - '1') * 8 + text[0] - 'a');
        return true;
    }

    /// <summary>Parses a lowercase UTF-8 square name. The provider is ignored.</summary>
    public static Square Parse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider = null)
    {
        return TryParse(utf8Text, provider, out var square)
            ? square
            : throw new FormatException("Expected a square a1 through h8.");
    }

    /// <summary>Tries to parse a lowercase UTF-8 square name.</summary>
    public static bool TryParse(ReadOnlySpan<byte> utf8Text, out Square square)
        => TryParse(utf8Text, null, out square);

    /// <summary>Tries to parse a lowercase UTF-8 square name. The provider is ignored.</summary>
    public static bool TryParse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider, out Square square)
    {
        square = default;
        if (utf8Text.Length != 2 || utf8Text[0] is < (byte)'a' or > (byte)'h'
            || utf8Text[1] is < (byte)'1' or > (byte)'8')
        {
            return false;
        }

        square = new((utf8Text[1] - '1') * 8 + utf8Text[0] - 'a');
        return true;
    }

    /// <summary>Writes a lowercase square name as UTF-8 without allocating a string.</summary>
    /// <remarks>Accepts an empty format or G/g and ignores the provider. Returns false without writing
    /// for an invalid square or insufficient space. Other formats throw <see cref="FormatException"/>.</remarks>
    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten,
        ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
    {
        bytesWritten = 0;
        if (!format.IsEmpty && !format.SequenceEqual("G") && !format.SequenceEqual("g"))
        {
            throw new FormatException("Only the general square format is supported.");
        }

        if (!IsValid || utf8Destination.Length < 2)
        {
            return false;
        }

        utf8Destination[0] = (byte)('a' + Index % 8);
        utf8Destination[1] = (byte)('1' + Index / 8);
        bytesWritten = 2;
        return true;
    }

    /// <inheritdoc />
    public override string ToString() => IsValid ? Core.Math.SquareOps.ToAbbreviature(Index) : string.Empty;
}
