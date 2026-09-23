using ChessRealms.Engine.Core.Constants;
using ChessRealms.Engine.Core.Math;
using ChessRealms.Engine.Core.Types;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace ChessRealms.Engine.Parsing;

internal static class FenStrings
{
    internal const string StartPosition = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

    internal static bool TryParse(string? fen, out Position position)
    {
        if (TryParseSyntax(fen, out var candidate) && PositionValidation.IsValid(candidate))
        {
            position = candidate;
            return true;
        }
        position = new();
        return false;
    }

    // Internal geometric move-generation fixtures may deliberately omit kings.
    // This entry point must never be used to construct a public game.
    internal static bool TryParseSyntax(string? fen, out Position position)
    {
        position = new();
        if (fen is null) return false;
        string[] fields = fen.Split(' ');
        if (fields.Length != 6 || fields.Any(string.IsNullOrEmpty)) return false;
        var ranks = fields[0].Split('/');
        if (ranks.Length != 8) return false;
        Position candidate = new();
        for (int rank = 0; rank < 8; rank++)
        {
            int file = 0;
            bool previousDigit = false;
            foreach (char c in ranks[rank])
            {
                if (c is >= '1' and <= '8')
                {
                    if (previousDigit) return false;
                    file += c - '0';
                    previousDigit = true;
                }
                else
                {
                    int piece = "pnbrqk".IndexOf(char.ToLowerInvariant(c));
                    if (piece < 0 || !"pnbrqkPNBRQK".Contains(c) || file >= 8) return false;
                    candidate.SetPieceAt((7 - rank) * 8 + file++, piece, char.IsUpper(c) ? Colors.White : Colors.Black);
                    previousDigit = false;
                }
                if (file > 8) return false;
            }
            if (file != 8) return false;
        }
        if (fields[1] is not ("w" or "b")) return false;
        candidate.color = fields[1] == "w" ? Colors.White : Colors.Black;
        if (fields[2] != "-")
        {
            int lastIndex = -1;
            foreach (char c in fields[2])
            {
                int index = "KQkq".IndexOf(c);
                if (index <= lastIndex) return false;
                candidate.castlings |= 1 << index;
                lastIndex = index;
            }
        }
        if (fields[3] != "-")
        {
            if (!AlgebraicNotation.TryParseSquare(fields[3], out candidate.enpassant)
                || fields[3][1] is not ('3' or '6')) return false;
        }
        if (!TryCounter(fields[4], out candidate.halfMoveClock)
            || !TryCounter(fields[5], out candidate.fullMoveCount) || candidate.fullMoveCount == 0) return false;
        position = candidate;
        return true;
    }

    private static bool TryCounter(string text, out BigInteger value)
    {
        value = 0;
        return text.All(c => c is >= '0' and <= '9')
            && BigInteger.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);
    }

    internal static string Format(Position position)
    {
        if (!PositionValidation.IsValid(position)) throw new ArgumentException("Invalid standard chess position.", nameof(position));
        return FormatUnchecked(position);
    }

    internal static string FormatUnchecked(Position position)
    {
        var result = new StringBuilder();
        for (int rank = 7; rank >= 0; rank--)
        {
            int empty = 0;
            for (int file = 0; file < 8; file++)
            {
                var piece = position.GetPieceAt(rank * 8 + file, Colors.White);
                if (!Piece.IsValid(piece)) piece = position.GetPieceAt(rank * 8 + file, Colors.Black);
                if (!Piece.IsValid(piece)) { empty++; continue; }
                if (empty != 0) { result.Append(empty); empty = 0; }
                char c = "pnbrqk"[piece.Value];
                result.Append(piece.Color == Colors.White ? char.ToUpperInvariant(c) : c);
            }
            if (empty != 0) result.Append(empty);
            if (rank != 0) result.Append('/');
        }
        result.Append(position.color == Colors.White ? " w " : " b ");
        if (position.castlings == Castlings.None) result.Append('-');
        else for (int i = 0; i < 4; i++) if ((position.castlings & (1 << i)) != 0) result.Append("KQkq"[i]);
        result.Append(' ').Append(position.enpassant == Squares.Empty ? "-" : SquareOps.ToAbbreviature(position.enpassant));
        result.Append(' ').Append(position.halfMoveClock.ToString(CultureInfo.InvariantCulture));
        result.Append(' ').Append(position.fullMoveCount.ToString(CultureInfo.InvariantCulture));
        return result.ToString();
    }
}
