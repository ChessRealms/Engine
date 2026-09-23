namespace ChessRealms.Engine;

/// <summary>A chess side, or no side for values such as an empty square.</summary>
public enum PieceColor
{
    None,
    White,
    Black
}

internal static class PieceColorConversions
{
    internal static PieceColor ToPublicColor(this int color) => color switch
    {
        Core.Constants.Colors.White => PieceColor.White,
        Core.Constants.Colors.Black => PieceColor.Black,
        _ => PieceColor.None
    };
}
