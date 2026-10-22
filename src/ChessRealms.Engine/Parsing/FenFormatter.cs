using ChessRealms.Engine.Core.Constants;
using ChessRealms.Engine.Core.Types;
using System.Buffers;
using System.Globalization;
using System.Numerics;

namespace ChessRealms.Engine.Parsing;

internal static class FenFormatter
{
    // 64 piece symbols, seven rank separators, side, castling and en passant fields.
    private const int MaxLengthWithoutCounters = 81;
    private const int StackBufferCapacity = 128;

    // Use this entry point when the caller has not validated the position.
    internal static string Format(Position position)
    {
        if (!PositionValidation.IsValid(position))
        {
            throw new ArgumentException("Invalid standard chess position.", nameof(position));
        }

        return FormatWithoutValidation(position);
    }

    // Skips PositionValidation. Game state has already passed it; geometric
    // test fixtures may deliberately omit kings or exceed playable piece counts.
    internal static string FormatWithoutValidation(Position position)
    {
        int capacity = GetMaxFormattedLength(position);
        char[]? rented = null;
        Span<char> text = capacity <= StackBufferCapacity
            ? stackalloc char[StackBufferCapacity]
            : (rented = ArrayPool<char>.Shared.Rent(capacity));
        try
        {
            int written = WriteFenToBuffer(position, text);
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

    // Like FormatWithoutValidation, but preserves the caller's entire span on
    // failure. Format into scratch space first, then copy only when it fits.
    internal static bool TryFormatWithoutValidation(Position position, Span<char> destination, out int charsWritten)
    {
        charsWritten = 0;
        int capacity = GetMaxFormattedLength(position);
        char[]? rented = null;
        Span<char> text = capacity <= StackBufferCapacity
            ? stackalloc char[StackBufferCapacity]
            : (rented = ArrayPool<char>.Shared.Rent(capacity));
        try
        {
            int written = WriteFenToBuffer(position, text);
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

    // An upper bound for scratch allocation, not the actual formatted length.
    // Counters are unbounded, so the stack buffer is only the common fast path.
    private static int GetMaxFormattedLength(Position position)
        => checked(MaxLengthWithoutCounters + 2
            + GetMaxCounterLength(position.halfMoveClock)
            + GetMaxCounterLength(position.fullMoveCount));

    // Since 10 > 2^3, bitLength / 3 + 1 safely bounds the decimal digits.
    // This integer bound also works at powers of ten without floating-point rounding.
    private static int GetMaxCounterLength(BigInteger value)
        => checked((int)(value.GetBitLength() / 3 + 1) + (value.Sign < 0 ? 1 : 0));

    // The caller provides scratch space sized by GetMaxFormattedLength.
    private static int WriteFenToBuffer(Position position, Span<char> destination)
    {
        int length = WriteFieldsWithoutCounters(position, destination);
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

    // Writes the first four fields without position validation. En passant is
    // preserved as stored; this is not a normalized repetition identity.
    internal static string FormatWithoutCounters(Position position)
    {
        Span<char> text = stackalloc char[MaxLengthWithoutCounters];
        int written = WriteFieldsWithoutCounters(position, text);
        return new string(text[..written]);
    }

    private static int WriteFieldsWithoutCounters(Position position, Span<char> destination)
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
