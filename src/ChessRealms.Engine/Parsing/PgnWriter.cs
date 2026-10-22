using ChessRealms.Engine.Core.Constants;
using ChessRealms.Engine.Core.Movements;
using System.Globalization;
using System.Numerics;

namespace ChessRealms.Engine.Parsing;

internal static class PgnWriter
{
    internal static void Write(PgnGame document, TextWriter writer)
    {
        foreach (string name in PgnTags.Roster)
        {
            WriteTag(writer, name, document.Tags[name]);
        }

        foreach (var pair in document.Tags.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            if (!PgnTags.Roster.Contains(pair.Key, StringComparer.Ordinal))
            {
                WriteTag(writer, pair.Key, pair.Value);
            }
        }

        writer.Write('\n');
        if (!FenStrings.TryParse(document.InitialFen, out var position))
        {
            throw new InvalidOperationException("The validated PGN starting position could not be loaded.");
        }

        // PgnGame owns an already validated line. Export needs board legality
        // and SAN, but no game history, undo stack or repetition accounting.
        int column = 0;
        Span<char> san = stackalloc char[SanFormatter.MaxLength];
        Span<int> legalMoves = stackalloc int[MoveGen.MaxMoves];
        for (int i = 0; i < document.Moves.Count; ++i)
        {
            if (position.color == Colors.White || i == 0)
            {
                WriteMoveNumber(writer, position.fullMoveCount, position.color == Colors.Black, ref column);
            }

            CoordinateMove move = document.Moves[i];
            int count = LegalMoves.Write(position, legalMoves);
            if (!LegalMoves.TryFind(move, legalMoves[..count], out int encodedMove))
            {
                throw new InvalidOperationException("The validated PGN line could not be replayed.");
            }

            int written = SanFormatter.Write(position, encodedMove, legalMoves[..count], san);
            WriteToken(writer, san[..written], ref column);
            MoveDriver.MakeMove(ref position, encodedMove);
            position.SwitchColor();
        }

        WriteToken(writer, PgnTags.ResultText(document.Result), ref column);
        writer.Write("\n\n");
    }

    private static void WriteMoveNumber(TextWriter writer, BigInteger number, bool black, ref int column)
    {
        Span<char> token = stackalloc char[32];
        ReadOnlySpan<char> suffix = black ? "..." : ".";
        if (number.TryFormat(token[..^3], out int written, provider: CultureInfo.InvariantCulture))
        {
            suffix.CopyTo(token[written..]);
            WriteToken(writer, token[..(written + suffix.Length)], ref column);
        }
        else
        {
            // Counters are unbounded. Keep the uncommon large-number path exact.
            string text = number.ToString(CultureInfo.InvariantCulture) + (black ? "..." : ".");
            WriteToken(writer, text, ref column);
        }
    }

    private static void WriteTag(TextWriter writer, string name, string value)
    {
        writer.Write('[');
        writer.Write(name);
        writer.Write(" \"");
        foreach (char c in value)
        {
            if (c is '\\' or '"')
            {
                writer.Write('\\');
            }

            writer.Write(c);
        }

        writer.Write("\"]\n");
    }

    private static void WriteToken(TextWriter writer, ReadOnlySpan<char> token, ref int column)
    {
        if (column != 0)
        {
            if (column + 1 + token.Length > 79)
            {
                writer.Write('\n');
                column = 0;
            }
            else
            {
                writer.Write(' ');
                ++column;
            }
        }

        writer.Write(token);
        column += token.Length;
    }
}
