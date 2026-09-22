using ChessRealms.Engine.Core.Constants;
using ChessRealms.Engine.Core.Extensions;
using ChessRealms.Engine.Core.Math;
using System.Text;

namespace ChessRealms.Engine.Debugs;

internal static partial class DebugHelper
{    
    public const string DEBUG = "DEBUG";

    public static string MoveToString(int move)
    {
        var sb = new StringBuilder();
        
        var srcP = BinaryMoveOps.DecodeSrcPiece(move);
        var srcC = BinaryMoveOps.DecodeSrcColor(move);

        var trg = BinaryMoveOps.DecodeTrg(move);
        var cap = BinaryMoveOps.DecodeCapture(move);

        char piece = srcP switch
        {
            Pieces.Pawn => 'p',
            Pieces.Knight => 'n',
            Pieces.Bishop => 'b',
            Pieces.Rook => 'r',
            Pieces.Queen => 'q',
            Pieces.King => 'k',
            _ => '*'
        };

        if (srcC == Colors.White) piece = char.ToUpper(piece);

        sb.Append(piece);
        
        if (cap == 1) 
            sb.Append('x');

        sb.Append(SquareOps.ToAbbreviature(trg));

        return sb.ToString();
    }

    public static string BitboardToString(ulong bitboard)
    {
        var sb = new StringBuilder();

        for (int r = 7; r >= 0; --r)
        {
            for (int f = 0; f < 8; ++f)
            {
                var s = SquareOps.FromFileRank(f, r);
                var b = SquareOps.ToBitboard(s);

                sb.Append(' ');
                sb.Append((bitboard & b).IsTrue() ? '*' : '.');
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }
}
