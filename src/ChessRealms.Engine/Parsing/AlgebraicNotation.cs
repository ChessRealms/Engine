using ChessRealms.Engine.Core.Constants;
using ChessRealms.Engine.Core.Math;

namespace ChessRealms.Engine.Parsing;

internal static class AlgebraicNotation
{
    internal static CoordinateMove ParseCoordinateMove(ReadOnlySpan<char> text)
        => TryParseCoordinateMove(text, out var move) ? move : throw new FormatException("Expected e2e4 or a7a8q/r/b/n.");

    internal static bool TryParseCoordinateMove(ReadOnlySpan<char> text, out CoordinateMove move)
    {
        move = default;
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
        move = new(new Square(src), new Square(trg), promotion);
        return true;
    }

    internal static int ParseSquare(ReadOnlySpan<char> text)
        => TryParseSquare(text, out int square) ? square : throw new FormatException("Expected a square a1 through h8.");

    internal static bool TryParseSquare(ReadOnlySpan<char> text, out int square)
    {
        square = Squares.Empty;
        if (text.Length != 2 || text[0] is < 'a' or > 'h' || text[1] is < '1' or > '8') return false;
        square = SquareOps.FromFileRank(text[0] - 'a', text[1] - '1');
        return true;
    }
}
