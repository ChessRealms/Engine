using System.Runtime.CompilerServices;

namespace ChessRealms.Engine.Core.Extensions;

internal static class CommonTypesExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsTrue(this int value)
    {
        return value != 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsTrue(this ulong value) 
    {
        return value != 0;
    }
}
