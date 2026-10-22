using ChessRealms.Engine.Core.Constants;
using ChessRealms.Engine.Core.Types;
using System.Globalization;
using System.Numerics;

namespace ChessRealms.Engine.Parsing;

internal static class FenParser
{
    // Parses all six fields, then checks the standard-chess position invariants.
    internal static bool TryParse(string? fen, out Position position)
    {
        if (TryParseSyntaxOnly(fen, out var candidate) && PositionValidation.IsValid(candidate))
        {
            position = candidate;
            return true;
        }

        position = new();
        return false;
    }

    // Internal geometric move-generation fixtures may deliberately omit kings.
    // This entry point must never be used to construct a public game.
    internal static bool TryParseSyntaxOnly(string? fen, out Position position)
    {
        position = new();
        if (fen is null)
        {
            return false;
        }

        string[] fields = fen.Split(' ');
        if (fields.Length != 6 || fields.Any(string.IsNullOrEmpty))
        {
            return false;
        }

        Position candidate = new();
        if (!TryParsePiecePlacement(fields[0], ref candidate))
        {
            return false;
        }

        if (fields[1] is not ("w" or "b"))
        {
            return false;
        }

        candidate.color = fields[1] == "w" ? Colors.White : Colors.Black;
        if (!TryParseCastlingRights(fields[2], out candidate.castlings))
        {
            return false;
        }

        if (!TryParseEnPassantSquare(fields[3], out candidate.enpassant))
        {
            return false;
        }

        if (!TryParseNonNegativeCounter(fields[4], out candidate.halfMoveClock)
            || !TryParseNonNegativeCounter(fields[5], out candidate.fullMoveCount)
            || candidate.fullMoveCount == 0)
        {
            return false;
        }

        position = candidate;
        return true;
    }

    private static bool TryParsePiecePlacement(string text, ref Position position)
    {
        var ranks = text.Split('/');
        if (ranks.Length != 8)
        {
            return false;
        }

        for (int rank = 0; rank < 8; rank++)
        {
            int file = 0;
            bool previousWasDigit = false;
            foreach (char symbol in ranks[rank])
            {
                if (symbol is >= '1' and <= '8')
                {
                    if (previousWasDigit)
                    {
                        return false;
                    }

                    file += symbol - '0';
                    previousWasDigit = true;
                }
                else
                {
                    int piece = "pnbrqk".IndexOf(char.ToLowerInvariant(symbol));
                    if (piece < 0 || !"pnbrqkPNBRQK".Contains(symbol) || file >= 8)
                    {
                        return false;
                    }

                    int square = (7 - rank) * 8 + file;
                    int color = char.IsUpper(symbol) ? Colors.White : Colors.Black;
                    file++;
                    position.SetPieceAt(square, piece, color);
                    previousWasDigit = false;
                }

                if (file > 8)
                {
                    return false;
                }
            }

            if (file != 8)
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryParseCastlingRights(string text, out int castlingRights)
    {
        castlingRights = Castlings.None;
        if (text == "-")
        {
            return true;
        }

        int lastIndex = -1;
        foreach (char symbol in text)
        {
            int index = "KQkq".IndexOf(symbol);
            if (index <= lastIndex)
            {
                return false;
            }

            castlingRights |= 1 << index;
            lastIndex = index;
        }

        return true;
    }

    private static bool TryParseEnPassantSquare(string text, out int square)
    {
        square = Squares.Empty;
        if (text == "-")
        {
            return true;
        }

        if (!Square.TryParse(text, out var enPassant) || text[1] is not ('3' or '6'))
        {
            return false;
        }

        square = enPassant.Index;
        return true;
    }

    private static bool TryParseNonNegativeCounter(string text, out BigInteger value)
    {
        value = 0;
        return text.All(c => c is >= '0' and <= '9')
            && BigInteger.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);
    }
}
