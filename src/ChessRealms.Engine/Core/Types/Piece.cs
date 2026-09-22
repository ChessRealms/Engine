using ChessRealms.Engine.Core.Constants;
using System.Runtime.CompilerServices;

namespace ChessRealms.Engine.Core.Types;

public readonly struct Piece(int piece, int color)
{
    public readonly int Value = piece;
    public readonly int Color = color;

    public static readonly Piece Empty = new(Pieces.None, Colors.None);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsValid(Piece piece)
    {
        return Pieces.IsValid(piece.Value) && Colors.IsValid(piece.Color);
    }
}
