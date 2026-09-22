using ChessRealms.Engine.Core.Constants;
using ChessRealms.Engine.Core.Math;
using System.Runtime.InteropServices;

namespace ChessRealms.Engine.Core.Attacks;

internal static unsafe class KingAttacks
{
    public static readonly ulong[] AttackMasks;
    public static readonly ulong* AttackMasksPtr;

    static KingAttacks()
    {
        AttackMasks = new ulong[64];

        for (int square = 0; square < 64; ++square)
        {
            AttackMasks[square] = MaskKingAttack(square);
        }

        AttackMasksPtr = (ulong*)GCHandle.Alloc(AttackMasks, GCHandleType.Pinned).AddrOfPinnedObject().ToPointer();
    }

    public static void InvokeInit()
    {
        _ = AttackMasks[0];
    }

    public static ulong MaskKingAttack(int square)
    {
        ulong board = SquareOps.ToBitboard(square);
        ulong attacks = 0UL;

        attacks |= board << 8;
        attacks |= board >> 8;

        attacks |= board << 9 & SquareMapping.NOT_A_FILE;
        attacks |= board << 1 & SquareMapping.NOT_A_FILE;
        attacks |= board >> 7 & SquareMapping.NOT_A_FILE;

        attacks |= board >> 9 & SquareMapping.NOT_H_FILE;
        attacks |= board >> 1 & SquareMapping.NOT_H_FILE;
        attacks |= board << 7 & SquareMapping.NOT_H_FILE;

        return attacks;
    }
}
