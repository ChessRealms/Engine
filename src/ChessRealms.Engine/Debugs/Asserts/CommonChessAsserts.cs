using ChessRealms.Engine.Core.Constants;
using System.Diagnostics;

namespace ChessRealms.Engine.Debugs;

internal static partial class DebugHelper
{
    public static partial class Assert
    {
        [Conditional(DEBUG)]
        public static void IsValidColor(int color)
        {
            Debug.Assert(Colors.IsValid(color), "Invalid Color.",
                "Actual color value: '{0}'.", color);
        }

        [Conditional(DEBUG)]
        public static void IsValidPiece(int piece)
        {
            Debug.Assert(Pieces.IsValid(piece), "Invalid Piece.",
                "Actual piece value: '{0}'.", piece);
        }

        [Conditional(DEBUG)]
        public static void IsLeapingPiece(int piece)
        {
            bool isLeaping = piece == Pieces.King 
                || piece == Pieces.Knight;

            Debug.Assert(isLeaping, "Invalid Leaping Piece.",
                "Actual piece value: '{0}'.", piece);
        }

        [Conditional(DEBUG)]
        public static void IsSlidingPiece(int piece)
        {
            bool isSliding = piece == Pieces.Bishop
                || piece == Pieces.Rook
                || piece == Pieces.Queen;

            Debug.Assert(isSliding, "Invalid Sliding Piece.",
                "Actual piece value: '{0}'.", piece);
        }

        [Conditional(DEBUG)]
        public static void IsValidSingleCastling(int castling)
        {
            Debug.Assert(Castlings.IsValidSingle(castling), "Invalid Single Castling.",
                "Actual castling value: '{0}'.", castling);
        }

        [Conditional(DEBUG)]
        public static void IsValidSquare(int square)
        {
            Debug.Assert(Squares.IsValid(square), "Invalid Square.",
                "Actual square value: '{0}'.", square);
        }
    }
}
