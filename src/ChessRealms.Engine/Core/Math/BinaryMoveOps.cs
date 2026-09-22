using ChessRealms.Engine.Core.Constants;
using System.Runtime.CompilerServices;

namespace ChessRealms.Engine.Core.Math;

internal static class BinaryMoveOps
{
    // 6 first bits filled with ones (63 as hexdecimal)
    public const int SrcSquare = 0x3f;

    // 6 bits (63 << 6)
    public const int TrgSquare = 0xfc0;

    // 3 bits (7 << 12)
    public const int SrcPiece = 0x7000;

    // 2 bit (3 << 15)
    public const int SrcColor = 0x18000;

    // TODO: Remove this if we going use copy 'position' to rollback moves.
    // 3 bits (7 << 17)
    public const int TrgPiece = 0xe0000;

    // TODO: Remove this if we going use copy 'position' to rollback moves.
    // 2 bit (3 << 20)
    public const int TrgColor = 0x300000;

    // 3 bits (7 << 22)
    public const int Promotion = 0x1c00000;

    // 1 bit (1 << 25)
    public const int Capture = 0x2000000;

    // 1 bit (1 << 26)
    public const int DoublePush = 0x4000000;

    // 1 bit (1 << 27)
    public const int Enpassant = 0x8000000;

    // 4 bit (15 << 28)
    public const int Castling = -268435456;

    public const int NoneMove = 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int EncodeMove(
        int srcSquare, 
        int srcPiece, 
        int srcColor,
        int trgSquare, 
        int trgPiece = Pieces.None, 
        int trgColor = Colors.None, 
        int capture = 0, 
        int promotion = 0,
        int enpassant = 0,
        int doublePush = 0, 
        int castling = 0)
    {
        return srcSquare |
            (trgSquare << 6) |
            (srcPiece << 12) |
            (srcColor << 15) |
            (trgPiece << 17) |
            (trgColor << 20) |
            (promotion << 22) |
            (capture << 25) |
            (doublePush << 26) |
            (enpassant << 27) |
            (castling << 28);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int DecodeSrc(int encodedMove)
    {
        return SrcSquare & encodedMove;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int DecodeSrcPiece(int encodedMove)
    {
        return (SrcPiece & encodedMove) >> 12;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int DecodeSrcColor(int encodedMove)
    {
        return (SrcColor & encodedMove) >> 15;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int DecodeTrg(int encodedMove)
    {
        return (TrgSquare & encodedMove) >> 6;
    }

    // TODO: Remove this if we going use copy 'position' to rollback moves.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int DecodeTrgPiece(int encodedMove)
    {
        return (TrgPiece & encodedMove) >> 17;
    }

    // TODO: Remove this if we going use copy 'position' to rollback moves.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int DecodeTrgColor(int encodedMove)
    {
        return (TrgColor & encodedMove) >> 20;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int DecodePromotion(int encodedMove)
    {
        return (Promotion & encodedMove) >> 22;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int DecodeCapture(int encodedMove)
    {
        return (Capture & encodedMove) >> 25;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int DecodeDoublePush(int encodedMove)
    {
        return (DoublePush & encodedMove) >> 26;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int DecodeEnpassant(int encodedMove)
    {
        return (Enpassant & encodedMove) >> 27;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int DecodeCastling(int encodedMove)
    {
        return unchecked((int)((uint)(Castling & encodedMove) >> 28));
    }
}
