using ChessRealms.Engine.Core.Constants;
using ChessRealms.Engine.Core.Math;
using ChessRealms.Engine.Parsing;

namespace ChessRealms.Engine;

/// <summary>A coordinate move. Promotion must be explicit; no implicit queen promotion.</summary>
public readonly record struct AlgebraicMove(Square Src, Square Trg, PieceValue Promotion = PieceValue.None)
{
    public Square Source => Src;
    public Square Target => Trg;
    public static readonly AlgebraicMove Empty = new(Squares.Empty, Squares.Empty);
    public bool IsValid() => Squares.IsValid(Src) && Squares.IsValid(Trg) && (int)Src != (int)Trg
        && Promotion is PieceValue.None or PieceValue.Queen or PieceValue.Rook or PieceValue.Bishop or PieceValue.Knight;
    public static AlgebraicMove Parse(ReadOnlySpan<char> span) => AlgebraicNotation.ParseAlgebraicMove(span);
    public static bool TryParse(ReadOnlySpan<char> span, out AlgebraicMove move)
        => AlgebraicNotation.TryParseAlgebraicMove(span, out move);
    public override string ToString() => !IsValid() ? "" :
        SquareOps.ToAbbreviature(Src) + SquareOps.ToAbbreviature(Trg) + (Promotion switch
        {
            PieceValue.Queen => "q", PieceValue.Rook => "r", PieceValue.Bishop => "b",
            PieceValue.Knight => "n", _ => ""
        });
}
