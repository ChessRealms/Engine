using ChessRealms.Engine.Core.Constants;
using ChessRealms.Engine.Core.Types;
using System.Buffers;
using System.Globalization;
using System.Numerics;

namespace ChessRealms.Engine.Parsing;

internal static class FenStrings
{
    internal const string StartPosition = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

    // 64 piece symbols, seven rank separators, side, castling and en passant fields.
    private const int MaxPositionFieldsLength = 81;
    private const int StackBufferLength = 128;

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

        if (!TryParseCounter(fields[4], out candidate.halfMoveClock)
            || !TryParseCounter(fields[5], out candidate.fullMoveCount)
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

    private static bool TryParseCounter(string text, out BigInteger value)
    {
        value = 0;
        return text.All(c => c is >= '0' and <= '9')
            && BigInteger.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);
    }

    internal static string Format(Position position)
    {
        if (!PositionValidation.IsValid(position))
        {
            throw new ArgumentException("Invalid standard chess position.", nameof(position));
        }

        return FormatUnchecked(position);
    }

    internal static string FormatUnchecked(Position position)
    {
        int capacity = GetBufferLength(position);
        char[]? rented = null;
        Span<char> text = capacity <= StackBufferLength
            ? stackalloc char[StackBufferLength]
            : (rented = ArrayPool<char>.Shared.Rent(capacity));
        try
        {
            int written = WriteUnchecked(position, text);
            return new string(text[..written]);
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<char>.Shared.Return(rented);
            }
        }
    }

    internal static bool TryFormatUnchecked(Position position, Span<char> destination, out int charsWritten)
    {
        charsWritten = 0;
        int capacity = GetBufferLength(position);
        char[]? rented = null;
        Span<char> text = capacity <= StackBufferLength
            ? stackalloc char[StackBufferLength]
            : (rented = ArrayPool<char>.Shared.Rent(capacity));
        try
        {
            int written = WriteUnchecked(position, text);
            if (!text[..written].TryCopyTo(destination))
            {
                return false;
            }

            charsWritten = written;
            return true;
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<char>.Shared.Return(rented);
            }
        }
    }

    private static int GetBufferLength(Position position)
        => checked(MaxPositionFieldsLength + 2
            + GetCounterBufferLength(position.halfMoveClock)
            + GetCounterBufferLength(position.fullMoveCount));

    // Since 10 > 2^3, bitLength / 3 + 1 safely bounds the decimal digits.
    // This integer bound also works at powers of ten without floating-point rounding.
    private static int GetCounterBufferLength(BigInteger value)
        => checked((int)(value.GetBitLength() / 3 + 1) + (value.Sign < 0 ? 1 : 0));

    private static int WriteUnchecked(Position position, Span<char> destination)
    {
        int length = WritePositionFields(position, destination);
        destination[length++] = ' ';
        length += WriteCounter(position.halfMoveClock, destination[length..]);
        destination[length++] = ' ';
        length += WriteCounter(position.fullMoveCount, destination[length..]);
        return length;
    }

    private static int WriteCounter(BigInteger value, Span<char> destination)
    {
        if (!value.TryFormat(destination, out int written, provider: CultureInfo.InvariantCulture))
        {
            throw new InvalidOperationException("The internal FEN buffer is too small.");
        }

        return written;
    }

    // The first four FEN fields identify a position without its move counters.
    internal static string FormatPositionFields(Position position)
    {
        Span<char> text = stackalloc char[MaxPositionFieldsLength];
        int written = WritePositionFields(position, text);
        return new string(text[..written]);
    }

    private static int WritePositionFields(Position position, Span<char> destination)
    {
        int length = 0;
        for (int rank = 7; rank >= 0; rank--)
        {
            int emptySquares = 0;
            for (int file = 0; file < 8; file++)
            {
                var piece = position.GetPieceAt(rank * 8 + file, Colors.White);
                if (!Piece.IsValid(piece))
                {
                    piece = position.GetPieceAt(rank * 8 + file, Colors.Black);
                }

                if (!Piece.IsValid(piece))
                {
                    emptySquares++;
                    continue;
                }

                if (emptySquares != 0)
                {
                    destination[length++] = (char)('0' + emptySquares);
                    emptySquares = 0;
                }

                char symbol = "pnbrqk"[piece.Value];
                destination[length++] = piece.Color == Colors.White ? char.ToUpperInvariant(symbol) : symbol;
            }

            if (emptySquares != 0)
            {
                destination[length++] = (char)('0' + emptySquares);
            }

            if (rank != 0)
            {
                destination[length++] = '/';
            }
        }

        destination[length++] = ' ';
        destination[length++] = position.color == Colors.White ? 'w' : 'b';
        destination[length++] = ' ';
        if (position.castlings == Castlings.None)
        {
            destination[length++] = '-';
        }
        else
        {
            for (int i = 0; i < 4; i++)
            {
                if ((position.castlings & (1 << i)) != 0)
                {
                    destination[length++] = "KQkq"[i];
                }
            }
        }

        destination[length++] = ' ';
        if (position.enpassant == Squares.Empty)
        {
            destination[length++] = '-';
        }
        else
        {
            destination[length++] = (char)('a' + position.enpassant % 8);
            destination[length++] = (char)('1' + position.enpassant / 8);
        }

        return length;
    }
}
