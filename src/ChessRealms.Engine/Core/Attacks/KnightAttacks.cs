using ChessRealms.Engine.Core.Constants;
using ChessRealms.Engine.Core.Math;
using System.Runtime.InteropServices;

namespace ChessRealms.Engine.Core.Attacks;

internal static unsafe class KnightAttacks
{
    public static readonly ulong[] AttackMasks;
    public static readonly ulong* AttackMasksPtr;

    static KnightAttacks()
    {
        AttackMasks = new ulong[64];

        for (int square = 0; square < 64; ++square)
        {
            AttackMasks[square] = MaskKnightAttack(square);
        }

        AttackMasksPtr = (ulong*)GCHandle.Alloc(AttackMasks, GCHandleType.Pinned).AddrOfPinnedObject().ToPointer();
    }

    /// <summary>
    /// Triggers static ctor().
    /// </summary>
    public static void InvokeInit()
    {
        // Touch variable to trigger static ctor.
        _ = AttackMasks[0];
    }

    public static ulong MaskKnightAttack(int square)
    {
        ulong board = SquareOps.ToBitboard(square);
        ulong attacks = 0UL;

        attacks |= board >> 17 & SquareMapping.NOT_H_FILE;
        attacks |= board >> 15 & SquareMapping.NOT_A_FILE;
        attacks |= board >> 10 & SquareMapping.NOT_HG_FILE;
        attacks |= board >> 6 & SquareMapping.NOT_AB_FILE;

        attacks |= board << 17 & SquareMapping.NOT_A_FILE;
        attacks |= board << 15 & SquareMapping.NOT_H_FILE;
        attacks |= board << 10 & SquareMapping.NOT_AB_FILE;
        attacks |= board << 6 & SquareMapping.NOT_HG_FILE;

        return attacks;
    }
}
