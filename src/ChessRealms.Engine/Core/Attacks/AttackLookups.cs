namespace ChessRealms.Engine.Core.Attacks;

internal static class AttackLookups
{
    public static unsafe void InvokeInit()
    {
        PawnAttacks.InvokeInit();
        KnightAttacks.InvokeInit();
        BishopAttacks.InvokeInit();
        RookAttacks.InvokeInit();
        KingAttacks.InvokeInit();
    }
}
