using System.Globalization;

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
        var replay = ChessGame.FromFen(document.InitialFen);
        int column = 0;
        Span<char> san = stackalloc char[7];
        for (int i = 0; i < document.Moves.Count; ++i)
        {
            if (replay.CurrentColor == PieceColor.White || i == 0)
            {
                string number = replay.FullmoveNumber.ToString(CultureInfo.InvariantCulture)
                    + (replay.CurrentColor == PieceColor.White ? "." : "...");
                WriteToken(writer, number, ref column);
            }

            CoordinateMove move = document.Moves[i];
            if (!replay.TryFormatSan(move, san, out int written))
            {
                throw new InvalidOperationException("The validated PGN line could not be replayed.");
            }

            WriteToken(writer, san[..written], ref column);
            replay.MakeMove(move);
        }

        WriteToken(writer, PgnTags.ResultText(document.Result), ref column);
        writer.Write("\n\n");
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
