namespace ChessRealms.Engine;

/// <summary>A square on a standard chess board.</summary>
public readonly record struct Square
{
    private readonly byte encodedIndex;

    internal Square(int index)
    {
        if (index is < 0 or >= 64) throw new ArgumentOutOfRangeException(nameof(index));
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
        if (text.Length != 2 || text[0] is < 'a' or > 'h' || text[1] is < '1' or > '8') return false;

        square = new((text[1] - '1') * 8 + text[0] - 'a');
        return true;
    }

    /// <inheritdoc />
    public override string ToString() => IsValid ? Core.Math.SquareOps.ToAbbreviature(Index) : string.Empty;
}
