using ChessRealms.Engine.Core.Constants;
using ChessRealms.Engine.Core.Math;
using ChessRealms.Engine.Parsing;

namespace ChessRealms.Engine;

/// <summary>A move written as source and target squares, with an explicit promotion when required.</summary>
public readonly record struct CoordinateMove
{
    /// <summary>Creates a coordinate move.</summary>
    /// <exception cref="ArgumentException">A square or promotion value is invalid.</exception>
    public CoordinateMove(Square source, Square target, PieceValue promotion = PieceValue.None)
    {
        if (!source.IsValid) throw new ArgumentException("A valid source square is required.", nameof(source));
        if (!target.IsValid) throw new ArgumentException("A valid target square is required.", nameof(target));
        if (source == target) throw new ArgumentException("Source and target squares must differ.", nameof(target));
        if (promotion is not (PieceValue.None or PieceValue.Queen or PieceValue.Rook
            or PieceValue.Bishop or PieceValue.Knight))
            throw new ArgumentException("Promotion must be a queen, rook, bishop, knight, or none.", nameof(promotion));

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
    public static CoordinateMove Parse(ReadOnlySpan<char> text) => AlgebraicNotation.ParseCoordinateMove(text);

    /// <summary>Tries to parse lowercase long algebraic/coordinate notation.</summary>
    public static bool TryParse(ReadOnlySpan<char> text, out CoordinateMove move)
        => AlgebraicNotation.TryParseCoordinateMove(text, out move);

    /// <inheritdoc />
    public override string ToString() => !IsValid ? string.Empty :
        SquareOps.ToAbbreviature(Source.Index) + SquareOps.ToAbbreviature(Target.Index) + (Promotion switch
        {
            PieceValue.Queen => "q",
            PieceValue.Rook => "r",
            PieceValue.Bishop => "b",
            PieceValue.Knight => "n",
            _ => string.Empty
        });
}
