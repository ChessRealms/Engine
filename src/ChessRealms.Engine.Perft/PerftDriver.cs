using ChessRealms.Engine.Core.Math;
using ChessRealms.Engine.Core.Movements;
using ChessRealms.Engine.Core.Types;
using System.Text;

namespace Perft
{
    public static class PerftDriver
    {
        public struct PerftResult
        {
            public ulong Nodes;
            public int Captures;
            public int Ep;
            public int Castles;
            public int Promotions;

            public override readonly string ToString()
            {
                var sb = new StringBuilder();
                sb.AppendLine(string.Format("Nodes: {0:n0}", Nodes));
                sb.AppendLine(string.Format("Captures: {0:n0}", Captures));
                sb.AppendLine(string.Format("Ep: {0:n0}", Ep));
                sb.AppendLine(string.Format("Castles: {0:n0}", Castles));
                sb.AppendLine(string.Format("Promotions: {0:n0}", Promotions));
                return sb.ToString();
            }
        }

        public static PerftResult Test(Position pos, int depth, bool upper = true)
        {
            Position tmpPos = new();
            Span<int> moves = stackalloc int[MoveGen.MaxMoves];

            int written = MoveGen.WriteMoves(
                ref pos, pos.color, moves);

            if (depth == 1)
            {
                PerftResult perftResult = new();

                for (int i = 0; i < written; ++i)
                {
                    tmpPos = pos;

                    MoveDriver.MakeMove(ref tmpPos, moves[i]);

                    if (tmpPos.IsKingChecked(tmpPos.color))
                    {
                        continue;
                    }

                    perftResult.Nodes += 1;

                    if (BinaryMoveOps.DecodeCapture(moves[i]) != 0)
                        perftResult.Captures += 1;

                    if (BinaryMoveOps.DecodeEnpassant(moves[i]) != 0)
                    {
                        perftResult.Ep += 1;
                    }

                    if (BinaryMoveOps.DecodeCastling(moves[i]) != 0)
                        perftResult.Castles += 1;

                    if (BinaryMoveOps.DecodePromotion(moves[i]) != 0)
                        perftResult.Promotions += 1;
                }

                return perftResult;
            }

            PerftResult finalRes = new();

            for (int i = 0; i < written; ++i)
            {
                tmpPos = pos;

                MoveDriver.MakeMove(ref tmpPos, moves[i]);

                if (tmpPos.IsKingChecked(tmpPos.color))
                {
                    continue;
                }

                tmpPos.SwitchColor();

                var tmpRes = Test(tmpPos, depth - 1, false);

                finalRes.Nodes += tmpRes.Nodes;
                finalRes.Captures += tmpRes.Captures;
                finalRes.Ep += tmpRes.Ep;
                finalRes.Castles += tmpRes.Castles;
                finalRes.Promotions += tmpRes.Promotions;

                if (upper)
                {
                    var src = SquareOps.ToAbbreviature(BinaryMoveOps.DecodeSrc(moves[i]));
                    var trg = SquareOps.ToAbbreviature(BinaryMoveOps.DecodeTrg(moves[i]));

                    Console.WriteLine("{0}{1} {2:n0}", src, trg, tmpRes.Nodes);
                }
            }

            return finalRes;
        }
    }
}
