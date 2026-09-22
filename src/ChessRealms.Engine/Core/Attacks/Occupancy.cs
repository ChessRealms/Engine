using ChessRealms.Engine.Core.Math;
using System.Runtime.CompilerServices;

namespace ChessRealms.Engine.Core.Attacks;

internal static class Occupancy
{
    /// <summary>
    /// Set occupancy by specified occupancy index and attackMask. 
    /// More details: <see href="https://www.chessprogramming.org/Sliding_Piece_Attacks#By_Occupancy_Lookup"/>
    /// </summary>
    /// <param name="occupancyIndex"> Occupancy zero-based index. </param>
    /// <param name="bitsCount"> Bits count of <paramref name="attackMask"/>. </param>
    /// <param name="attackMask"> Attack mask. </param>
    /// <returns> Occupancy BitBoard. </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong CreateAtIndex(int occupancyIndex, int bitsCount, ulong attackMask)
    {
        ulong occupancy = 0;

        for (int i = 0; i < bitsCount; ++i)
        {
            int square = BitboardOps.Lsb(attackMask);
            attackMask = BitboardOps.PopBitAt(attackMask, square);

            if ((occupancyIndex & 1 << i) != 0)
                occupancy = BitboardOps.SetBitAt(occupancy, square);
        }

        return occupancy;
    }
}
