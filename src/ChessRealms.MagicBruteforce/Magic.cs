using ChessRealms.Engine.Core.Attacks;
using System.Numerics;

namespace ChessRealms.MagicBruteforce;

static class Magic
{
    private static ulong NextMagic(Random rnd)
    {
        return (ulong)(rnd.NextInt64() & rnd.NextInt64() & rnd.NextInt64());
    }

    public static ulong FindMagic(int square, int iterations, bool isBishop, Random rnd)
    {
        ulong[] occupancies = new ulong[4096];
        ulong[] attacks = new ulong[4096];
        ulong[] usedAttacks = new ulong[4096];

        ulong attackMask;
        int relevantBits;

        if (isBishop)
        {
            attackMask = BishopAttacks.AttackMasks[square];
            relevantBits = BishopAttacks.RelevantBits[square];
        }
        else
        {
            attackMask = RookAttacks.AttackMasks[square];
            relevantBits = RookAttacks.RelevantBits[square];
        }

        int occupancyIndicies = 1 << relevantBits;

        for (int i = 0; i < occupancyIndicies; ++i)
        {
            occupancies[i] = Occupancy.CreateAtIndex(i, relevantBits, attackMask);

            if (isBishop)
            {
                attacks[i] = BishopAttacks.MaskBishopSliderAttackOnTheFly(square, occupancies[i]);
            }
            else
            {
                attacks[i] = RookAttacks.MaskRookSliderAttackOnTheFly(square, occupancies[i]);
            }
        }

        for (int i = 0; i < iterations; ++i)
        {
            ulong magicNum = NextMagic(rnd);

            if (BitOperations.PopCount((attackMask * magicNum) & 0xFF00000000000000) < 6)
            {
                continue;
            }

            Array.Clear(usedAttacks);

            int index;
            bool fail;

            for (index = 0, fail = false; !fail && index < occupancyIndicies; ++index)
            {
                uint magicIndex = (uint)((occupancies[index] * magicNum) >> (64 - relevantBits));

                if (usedAttacks[magicIndex] == 0)
                {
                    usedAttacks[magicIndex] = attacks[index];
                }
                else if (usedAttacks[magicIndex] != attacks[index])
                {
                    fail = true;
                }
            }

            if (!fail)
            {
                return magicNum;
            }
        }

        return 0;
    }
}
