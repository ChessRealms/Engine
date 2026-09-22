namespace ChessRealms.Engine;

public readonly struct ChessPiece(PieceColor color, PieceValue piece)
{
    public readonly PieceColor Color = color;
    public readonly PieceValue Value = piece;

    public readonly bool IsEmpty() => Color == PieceColor.None && Value == PieceValue.None;

    public static readonly ChessPiece Empty = new(PieceColor.None, PieceValue.None);
}
