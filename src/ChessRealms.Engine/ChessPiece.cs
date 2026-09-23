namespace ChessRealms.Engine;

/// <summary>A piece on a board square. The default value represents an empty square.</summary>
public readonly record struct ChessPiece
{
    /// <summary>Creates a non-empty chess piece.</summary>
    public ChessPiece(PieceColor color, PieceValue value)
    {
        if (color is not (PieceColor.White or PieceColor.Black))
            throw new ArgumentException("A piece must be white or black.", nameof(color));
        if (value is < PieceValue.Pawn or > PieceValue.King)
            throw new ArgumentException("A valid piece value is required.", nameof(value));

        Color = color;
        Value = value;
    }

    /// <summary>The piece's side, or <see cref="PieceColor.None"/> when empty.</summary>
    public PieceColor Color { get; }

    /// <summary>The piece kind, or <see cref="PieceValue.None"/> when empty.</summary>
    public PieceValue Value { get; }

    /// <summary>Whether the square contains no piece.</summary>
    public bool IsEmpty => this == Empty;

    /// <summary>An empty board square.</summary>
    public static ChessPiece Empty => default;
}
