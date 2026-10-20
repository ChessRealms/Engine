namespace ChessRealms.Engine.Parsing;

internal static class PgnTags
{
    internal static readonly string[] Roster = ["Event", "Site", "Date", "Round", "White", "Black", "Result"];

    internal static bool IsValidName(string? name)
    {
        if (string.IsNullOrEmpty(name) || name.Length > 255 || !char.IsAsciiLetterOrDigit(name[0]))
        {
            return false;
        }

        foreach (char c in name)
        {
            if (!char.IsAsciiLetterOrDigit(c) && c != '_')
            {
                return false;
            }
        }

        return true;
    }

    internal static bool IsValidValue(string? value)
    {
        if (value is null || value.Length > 255)
        {
            return false;
        }

        for (int i = 0; i < value.Length; ++i)
        {
            char c = value[i];
            if (char.IsControl(c) || c is '\u2028' or '\u2029')
            {
                return false;
            }

            if (char.IsSurrogate(c))
            {
                if (!char.IsHighSurrogate(c) || i + 1 == value.Length || !char.IsLowSurrogate(value[++i]))
                {
                    return false;
                }
            }
        }

        return true;
    }

    internal static bool TryReadSetup(IReadOnlyDictionary<string, string> tags, out string? fen)
    {
        bool hasFen = tags.TryGetValue("FEN", out fen);
        bool hasSetup = tags.TryGetValue("SetUp", out var setup);
        return hasFen ? hasSetup && setup == "1" : !hasSetup || setup == "0";
    }

    internal static Dictionary<string, string> Normalize(Dictionary<string, string> tags, string initialFen, PgnResult result)
    {
        var normalized = new Dictionary<string, string>(tags, StringComparer.Ordinal);
        foreach (string name in Roster)
        {
            normalized.TryAdd(name, name == "Date" ? "????.??.??" : "?");
        }

        normalized["Result"] = ResultText(result);
        if (initialFen != ChessGame.StartingFen)
        {
            normalized["SetUp"] = "1";
            normalized["FEN"] = initialFen;
        }
        else
        {
            normalized.Remove("SetUp");
            normalized.Remove("FEN");
        }

        return normalized;
    }

    internal static string ResultText(PgnResult result) => result switch
    {
        PgnResult.WhiteWin => "1-0",
        PgnResult.BlackWin => "0-1",
        PgnResult.Draw => "1/2-1/2",
        _ => "*"
    };

    internal static PgnResult FromOutcome(GameResult result) => result switch
    {
        GameResult.WhiteWin => PgnResult.WhiteWin,
        GameResult.BlackWin => PgnResult.BlackWin,
        GameResult.Draw => PgnResult.Draw,
        _ => PgnResult.Unknown
    };

    internal static bool TryReadResult(ReadOnlySpan<char> text, out PgnResult result)
    {
        result = PgnResult.Unknown;
        if (text.SequenceEqual("*"))
        {
            return true;
        }

        if (text.SequenceEqual("1-0"))
        {
            result = PgnResult.WhiteWin;
            return true;
        }

        if (text.SequenceEqual("0-1"))
        {
            result = PgnResult.BlackWin;
            return true;
        }

        if (text.SequenceEqual("1/2-1/2"))
        {
            result = PgnResult.Draw;
            return true;
        }

        return false;
    }
}
