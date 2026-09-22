using ChessRealms.Engine.Core.Constants;
using ChessRealms.Engine.Core.Math;

namespace ChessRealms.Engine.Parsing;

public static class AlgebraicNotation
{
    public static AlgebraicMove ParseAlgebraicMove(ReadOnlySpan<char> text)
        => TryParseAlgebraicMove(text, out var move) ? move : throw new FormatException("Expected e2e4 or a7a8q/r/b/n.");

    public static bool TryParseAlgebraicMove(ReadOnlySpan<char> text, out AlgebraicMove move)
    {
        move = AlgebraicMove.Empty;
        if (text.Length is not (4 or 5) || !TryParseSquare(text[..2], out int src)
            || !TryParseSquare(text.Slice(2, 2), out int trg) || src == trg) return false;
        PieceValue promotion = PieceValue.None;
        if (text.Length == 5)
        {
            promotion = text[4] switch
            {
                'q' => PieceValue.Queen, 'r' => PieceValue.Rook, 'b' => PieceValue.Bishop,
                'n' => PieceValue.Knight, _ => PieceValue.None
            };
            if (promotion == PieceValue.None) return false;
        }
        move = new(src, trg, promotion);
        return true;
    }

    public static int ParseSquare(ReadOnlySpan<char> text)
        => TryParseSquare(text, out int square) ? square : throw new FormatException("Expected a square a1 through h8.");

    public static bool TryParseSquare(ReadOnlySpan<char> text, out int square)
    {
        square = Squares.Empty;
        if (text.Length != 2 || text[0] is < 'a' or > 'h' || text[1] is < '1' or > '8') return false;
        square = SquareOps.FromFileRank(text[0] - 'a', text[1] - '1');
        return true;
    }
}
