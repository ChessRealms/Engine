namespace ChessRealms.Engine;

/// <summary>A chess piece kind, or no piece for an empty square.</summary>
public enum PieceValue
{
    None,
    Pawn,
    Knight,
    Bishop,
    Rook,
    Queen,
    King
}

internal static class PieceValueConversions
{
    internal static PieceValue ToPublicPiece(this int piece) => piece switch
    {
        Core.Constants.Pieces.Pawn => PieceValue.Pawn,
        Core.Constants.Pieces.Knight => PieceValue.Knight,
        Core.Constants.Pieces.Bishop => PieceValue.Bishop,
        Core.Constants.Pieces.Rook => PieceValue.Rook,
        Core.Constants.Pieces.Queen => PieceValue.Queen,
        Core.Constants.Pieces.King => PieceValue.King,
        _ => PieceValue.None
    };

    internal static PieceValue ToPublicPromotion(this int promotion) => promotion switch
    {
        Core.Constants.Promotions.Knight => PieceValue.Knight,
        Core.Constants.Promotions.Bishop => PieceValue.Bishop,
        Core.Constants.Promotions.Rook => PieceValue.Rook,
        Core.Constants.Promotions.Queen => PieceValue.Queen,
        _ => PieceValue.None
    };
}
