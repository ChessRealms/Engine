using ChessRealms.Engine.Core.Math;
using ChessRealms.Engine.Debugs;
using System.Runtime.InteropServices;

namespace ChessRealms.Engine.Core.Attacks;

internal static unsafe class BishopAttacks
{
    /// <summary>
    /// Pre-calculated bishop slider attacks. Shape of array is <c>[64 * 512].</c>
    /// </summary>
    public static readonly ulong[] SliderAttacks;
    public static readonly ulong* SliderAttacksPtr;

    /// <summary>
    /// Pre-calculated bishop attack masks (no outer squares) for each square index from 0 to 63.
    /// </summary>
    public static readonly ulong[] AttackMasks;
    public static readonly ulong* AttackMasksPtr;

    public static readonly int[] RelevantBits = 
    [
         6, 5, 5, 5, 5, 5, 5, 6,
         5, 5, 5, 5, 5, 5, 5, 5,
         5, 5, 7, 7, 7, 7, 5, 5,
         5, 5, 7, 9, 9, 7, 5, 5,
         5, 5, 7, 9, 9, 7, 5, 5,
         5, 5, 7, 7, 7, 7, 5, 5,
         5, 5, 5, 5, 5, 5, 5, 5,
         6, 5, 5, 5, 5, 5, 5, 6
    ];
    public static readonly int* RelevantBitsPtr;

    public static readonly ulong[] MagicNumbers = 
    [
        0x0002100148088080,
        0x24502C0B20620000,
        0x0284280091000010,
        0x0011040081802280,
        0x0004050480220050,
        0x600088A008000410,
        0x0004880110500008,
        0x4306020062023000,
        0x0610626024009280,
        0x00B0100118090041,
        0x002012140442008D,
        0x0002080849000011,
        0x0469240420020000,
        0x4048042424400244,
        0x0000020202024000,
        0x4000810120904420,
        0x0820010802840840,
        0x603010080148208A,
        0x0942009020811500,
        0x101020280A404020,
        0x000E000400A20001,
        0x2803400808080410,
        0x00C4040444540408,
        0x0222008080840120,
        0x0402110408200860,
        0x000A20109001B204,
        0x40102080B0010840,
        0x2008080000220020,
        0x0010040018802102,
        0x011805082E008200,
        0x000C0400A0413420,
        0x00A20140009400A8,
        0x1801042004410849,
        0x0424010984A00200,
        0x0100404800500020,
        0x0026020080880080,
        0x01240484008A0102,
        0x1014080020021002,
        0x020200C404160601,
        0x06080101050020A0,
        0x0014040406004004,
        0x2000823002201021,
        0x4004820801000201,
        0x00008B0411084800,
        0x4610401009080081,
        0x000222300A081100,
        0x0204280485000410,
        0x0450088A0040428C,
        0x0004010808046000,
        0x0046211802110402,
        0x0000002C02080400,
        0x0000000820880200,
        0x0000201042120000,
        0x0010400801011508,
        0x0004049808250040,
        0x0609180088820010,
        0x0001018804220244,
        0x2000314206101244,
        0x0400000100491020,
        0x2840010412104424,
        0x00080882C1082200,
        0x00A0C0200A020618,
        0x1050208809080492,
        0x0010E008004C4441
    ];
    public static readonly ulong* MagicNumbersPtr;

    static BishopAttacks()
    {
        AttackMasks = new ulong[64];
        SliderAttacks = new ulong[64 * 512];

        for (int square = 0; square < 64; ++square)
        {
            AttackMasks[square] = MaskBishopAttack(square);
            
            int occupancyIndicies = 1 << RelevantBits[square];

            for (int index = 0; index < occupancyIndicies; ++index)
            {
                ulong occupancy = Occupancy.CreateAtIndex(index, RelevantBits[square], AttackMasks[square]);
                
                int magicIndex = (int)((occupancy * MagicNumbers[square]) >> (64 - RelevantBits[square]));

                ulong mask = MaskBishopSliderAttackOnTheFly(square, occupancy);

                SliderAttacks[(square * 512) + magicIndex] = mask;
            }
        }

        AttackMasksPtr = (ulong*)GCHandle.Alloc(AttackMasks, GCHandleType.Pinned).AddrOfPinnedObject().ToPointer();
        SliderAttacksPtr = (ulong*)GCHandle.Alloc(SliderAttacks, GCHandleType.Pinned).AddrOfPinnedObject().ToPointer();
        RelevantBitsPtr = (int*)GCHandle.Alloc(RelevantBits, GCHandleType.Pinned).AddrOfPinnedObject().ToPointer();
        MagicNumbersPtr = (ulong*)GCHandle.Alloc(MagicNumbers, GCHandleType.Pinned).AddrOfPinnedObject().ToPointer();
    }

    public static void InvokeInit()
    {
        _ = SliderAttacks[0];
    }

    /// <summary>
    /// Get bishop slider attack for specified square and occupancy.
    /// </summary>
    /// <param name="square"> Index of square at chessboard. </param>
    /// <param name="occupancy"> Occupancy at chessboard. </param>
    /// <returns> Attack mask. </returns>
    public static ulong GetSliderAttack(int square, ulong occupancy)
    {
        DebugHelper.Assert.IsValidSquare(square);

        occupancy &= AttackMasksPtr[square];
        occupancy *= MagicNumbersPtr[square];
        occupancy >>= 64 - RelevantBitsPtr[square];
        return SliderAttacksPtr[(square * 512) + unchecked((int)occupancy)];
    }

    /// <summary>
    /// Create bishop mask (without outer squares) for specified index.
    /// </summary>
    /// <param name="square"> Index of square at chessboard. </param>
    /// <returns> Attack mask (no outer squares). </returns>
    public static ulong MaskBishopAttack(int square)
    {
        ulong attacks = 0UL;
        int r, f;

        r = SquareOps.Rank(square) + 1;
        f = SquareOps.File(square) + 1;
        
        while (r <= 6 && f <= 6)
            attacks |= SquareOps.ToBitboard(SquareOps.FromFileRank(f++, r++));

        r = SquareOps.Rank(square) - 1;
        f = SquareOps.File(square) + 1;
        
        while (r >= 1 && f <= 6)
            attacks |= SquareOps.ToBitboard(SquareOps.FromFileRank(f++, r--));

        r = SquareOps.Rank(square) + 1;
        f = SquareOps.File(square) - 1;
        
        while (r <= 6 && f >= 1)
            attacks |= SquareOps.ToBitboard(SquareOps.FromFileRank(f--, r++));

        r = SquareOps.Rank(square) - 1;
        f = SquareOps.File(square) - 1;

        while (r >= 1 && f >= 1)
            attacks |= SquareOps.ToBitboard(SquareOps.FromFileRank(f--, r--));

        return attacks;
    }

    /// <summary>
    /// Create bishop mask (with outer squares) for specified square index and blockers.
    /// </summary>
    /// <param name="square"> Index of square at chessboard. </param>
    /// <param name="blockers"> Board of blockers. </param>
    /// <returns> Attack mask. </returns>
    public static ulong MaskBishopSliderAttackOnTheFly(int square, ulong blockers)
    {
        ulong attacks = 0UL;
        int r, f;

        r = SquareOps.Rank(square) + 1;
        f = SquareOps.File(square) + 1;

        while (r <= 7 && f <= 7)
        {
            ulong squareAtBoard = SquareOps.ToBitboard(SquareOps.FromFileRank(f++, r++));
            attacks |= squareAtBoard;
            if ((squareAtBoard & blockers) != 0)
                break;
        }

        r = SquareOps.Rank(square) - 1;
        f = SquareOps.File(square) + 1;

        while (r >= 0 && f <= 7)
        {
            ulong squareAtBoard = SquareOps.ToBitboard(SquareOps.FromFileRank(f++, r--));
            attacks |= squareAtBoard;
            if ((squareAtBoard & blockers) != 0)
                break;
        }

        r = SquareOps.Rank(square) + 1;
        f = SquareOps.File(square) - 1;

        while (r <= 7 && f >= 0)
        {
            ulong squareAtBoard = SquareOps.ToBitboard(SquareOps.FromFileRank(f--, r++));
            attacks |= squareAtBoard;
            if ((squareAtBoard & blockers) != 0)
                break;
        }

        r = SquareOps.Rank(square) - 1;
        f = SquareOps.File(square) - 1;

        while (r >= 0 && f >= 0)
        {
            ulong squareAtBoard = SquareOps.ToBitboard(SquareOps.FromFileRank(f--, r--));
            attacks |= squareAtBoard;
            if ((squareAtBoard & blockers) != 0)
                break;
        }

        return attacks;
    }
}
