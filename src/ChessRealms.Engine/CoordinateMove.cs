using ChessRealms.Engine.Core.Math;
using ChessRealms.Engine.Json;
using System.Text.Json.Serialization;

namespace ChessRealms.Engine;

/// <summary>A move written as source and target squares, with an explicit promotion when required.</summary>
[JsonConverter(typeof(CoordinateMoveConverter))]
public readonly record struct CoordinateMove : IUtf8SpanFormattable, IUtf8SpanParsable<CoordinateMove>
{
    /// <summary>Creates a coordinate move.</summary>
    /// <exception cref="ArgumentException">A square or promotion value is invalid.</exception>
    public CoordinateMove(Square source, Square target, PieceValue promotion = PieceValue.None)
    {
        if (!source.IsValid)
        {
            throw new ArgumentException("A valid source square is required.", nameof(source));
        }

        if (!target.IsValid)
        {
            throw new ArgumentException("A valid target square is required.", nameof(target));
        }

        if (source == target)
        {
            throw new ArgumentException("Source and target squares must differ.", nameof(target));
        }

        if (promotion is not (PieceValue.None or PieceValue.Queen or PieceValue.Rook
            or PieceValue.Bishop or PieceValue.Knight))
        {
            throw new ArgumentException("Promotion must be a queen, rook, bishop, knight, or none.", nameof(promotion));
        }

        Source = source;
        Target = target;
        Promotion = promotion;
    }

    /// <summary>The square the moving piece leaves.</summary>
    public Square Source { get; }

    /// <summary>The square the moving piece enters.</summary>
    public Square Target { get; }

    /// <summary>The promoted piece, or <see cref="PieceValue.None"/> for a non-promotion move.</summary>
    public PieceValue Promotion { get; }

    internal bool IsValid => Source.IsValid && Target.IsValid && Source != Target
        && Promotion is PieceValue.None or PieceValue.Queen or PieceValue.Rook or PieceValue.Bishop or PieceValue.Knight;

    /// <summary>Parses lowercase long algebraic/coordinate notation such as <c>e2e4</c> or <c>a7a8q</c>.</summary>
    public static CoordinateMove Parse(ReadOnlySpan<char> text)
    {
        return TryParse(text, out var move)
            ? move
            : throw new FormatException("Expected e2e4 or a7a8q/r/b/n.");
    }

    /// <summary>Tries to parse lowercase long algebraic/coordinate notation.</summary>
    public static bool TryParse(ReadOnlySpan<char> text, out CoordinateMove move)
    {
        move = default;

        if (text.Length is not (4 or 5))
        {
            return false;
        }

        if (!Square.TryParse(text[..2], out var source))
        {
            return false;
        }

        if (!Square.TryParse(text.Slice(2, 2), out var target))
        {
            return false;
        }

        if (source == target)
        {
            return false;
        }

        PieceValue promotion = PieceValue.None;
        if (text.Length == 5)
        {
            promotion = text[4] switch
            {
                'q' => PieceValue.Queen,
                'r' => PieceValue.Rook,
                'b' => PieceValue.Bishop,
                'n' => PieceValue.Knight,
                _ => PieceValue.None
            };

            if (promotion == PieceValue.None)
            {
                return false;
            }
        }

        move = new(source, target, promotion);
        return true;
    }

    /// <summary>Parses lowercase UTF-8 coordinate notation. The provider is ignored.</summary>
    public static CoordinateMove Parse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider = null)
    {
        return TryParse(utf8Text, provider, out var move)
            ? move
            : throw new FormatException("Expected e2e4 or a7a8q/r/b/n.");
    }

    /// <summary>Tries to parse lowercase UTF-8 coordinate notation.</summary>
    public static bool TryParse(ReadOnlySpan<byte> utf8Text, out CoordinateMove move)
        => TryParse(utf8Text, null, out move);

    /// <summary>Tries to parse lowercase UTF-8 coordinate notation. The provider is ignored.</summary>
    public static bool TryParse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider, out CoordinateMove move)
    {
        move = default;
        if (utf8Text.Length is not (4 or 5))
        {
            return false;
        }

        // Coordinate notation is ASCII; the character parser rejects every non-ASCII byte.
        Span<char> text = stackalloc char[utf8Text.Length];
        for (int i = 0; i < utf8Text.Length; ++i)
        {
            text[i] = (char)utf8Text[i];
        }

        return TryParse(text, out move);
    }

    /// <summary>Writes lowercase coordinate notation as UTF-8 without allocating a string.</summary>
    /// <remarks>Accepts an empty format or G/g and ignores the provider. Returns false without writing
    /// for an invalid move or insufficient space. Other formats throw <see cref="FormatException"/>.</remarks>
    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten,
        ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
    {
        bytesWritten = 0;
        if (!format.IsEmpty && !format.SequenceEqual("G") && !format.SequenceEqual("g"))
        {
            throw new FormatException("Only the general coordinate move format is supported.");
        }

        int length = Promotion == PieceValue.None ? 4 : 5;
        if (!IsValid || utf8Destination.Length < length)
        {
            return false;
        }

        Source.TryFormat(utf8Destination[..2], out _);
        Target.TryFormat(utf8Destination.Slice(2, 2), out _);
        if (Promotion != PieceValue.None)
        {
            utf8Destination[4] = Promotion switch
            {
                PieceValue.Queen => (byte)'q',
                PieceValue.Rook => (byte)'r',
                PieceValue.Bishop => (byte)'b',
                PieceValue.Knight => (byte)'n',
                _ => throw new InvalidOperationException("Invalid promotion piece.")
            };
        }

        bytesWritten = length;
        return true;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        if (!IsValid)
        {
            return string.Empty;
        }

        string promotionSuffix = Promotion switch
        {
            PieceValue.Queen => "q",
            PieceValue.Rook => "r",
            PieceValue.Bishop => "b",
            PieceValue.Knight => "n",
            _ => string.Empty
        };

        return SquareOps.ToAbbreviature(Source.Index) + SquareOps.ToAbbreviature(Target.Index) + promotionSuffix;
    }
}
