using ChessRealms.Engine.Core.Math;

namespace ChessRealms.Engine.Tests.Extensions;

internal static class SquareIndexExtensions
{
    public static ulong ToBitboard(this IEnumerable<int> squares)
    {
        return squares
            .Select(SquareOps.ToBitboard)
            .Aggregate((b1, b2) => b1 | b2);
    }
}
