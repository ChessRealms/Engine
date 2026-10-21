using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;

namespace ChessRealms.Engine.Parsing;

internal ref struct PgnReader(ReadOnlySpan<char> text)
{
    private readonly ReadOnlySpan<char> _text = text;
    private readonly Dictionary<string, string> _tags = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _tagOffsets = new(StringComparer.Ordinal);
    private int _position;
    private PgnDiagnostic? _error;

    internal static bool TryRead(ReadOnlySpan<char> text, [NotNullWhen(true)] out PgnGame? document,
        [NotNullWhen(false)] out PgnDiagnostic? diagnostic)
    {
        var reader = new PgnReader(text);
        bool success = reader.Read(out document);
        diagnostic = reader._error;
        return success;
    }

    private bool Read([NotNullWhen(true)] out PgnGame? document)
    {
        document = null;
        if (!_text.IsEmpty && _text[0] == '\uFEFF')
        {
            ++_position;
        }

        SkipWhiteSpace();
        while (_position < _text.Length && _text[_position] == '[')
        {
            if (!ReadTag())
            {
                return false;
            }

            SkipWhiteSpace();
        }

        if (_tags.TryGetValue("Variant", out var variant) && variant != "Standard")
        {
            return Fail(PgnErrorCode.UnsupportedVariant, _tagOffsets["Variant"], null, "Only standard chess is supported.");
        }

        if (!PgnTags.TryReadSetup(_tags, out var fen))
        {
            int offset = _tagOffsets.GetValueOrDefault("SetUp", _tagOffsets.GetValueOrDefault("FEN"));
            return Fail(PgnErrorCode.InvalidSetup, offset, null, "FEN requires SetUp \"1\", and SetUp \"1\" requires FEN.");
        }

        if (!ChessGame.TryCreateFromFen(fen ?? ChessGame.StartingFen, out var game))
        {
            return Fail(PgnErrorCode.InvalidFen, _tagOffsets["FEN"], null, "The starting FEN is invalid.");
        }

        PgnResult? headerResult = null;
        if (_tags.TryGetValue("Result", out var marker))
        {
            if (!PgnTags.TryReadResult(marker, out var result))
            {
                return Fail(PgnErrorCode.InvalidResult, _tagOffsets["Result"], null, "Invalid Result tag.");
            }

            headerResult = result;
        }

        int ply = 0;
        int? pendingNumber = null;
        while (true)
        {
            SkipWhiteSpace();
            if (_position == _text.Length)
            {
                return Fail(PgnErrorCode.MissingResult, _position, ply, "A game termination marker is required.");
            }

            int start = _position;
            if (RejectUnsupported(ply))
            {
                return false;
            }

            if (_text[_position] == '*')
            {
                ++_position;
            }
            else
            {
                while (_position < _text.Length && IsSymbolCharacter(_text[_position]))
                {
                    ++_position;
                }
            }

            var token = _text[start.._position];
            if (token.IsEmpty)
            {
                return Fail(PgnErrorCode.UnexpectedToken, start, ply, "Expected SAN, a move number or a result.");
            }

            if (token.Length > 255)
            {
                return Fail(PgnErrorCode.UnexpectedToken, start, ply, "A PGN symbol cannot exceed 255 characters.");
            }

            if (PgnTags.TryReadResult(token, out var result))
            {
                if (pendingNumber.HasValue)
                {
                    return Fail(PgnErrorCode.InvalidMoveNumber, pendingNumber.Value, ply, "A move number must precede a move.");
                }

                if ((headerResult.HasValue && result != headerResult.Value)
                    || (game.IsFinished && result != PgnTags.FromOutcome(game.Outcome.Result)))
                {
                    return Fail(PgnErrorCode.ResultMismatch, start, ply, "The result conflicts with the Result tag or finished position.");
                }

                SkipWhiteSpace();
                if (_position < _text.Length)
                {
                    if (RejectUnsupported(ply))
                    {
                        return false;
                    }

                    return Fail(_text[_position] == '[' ? PgnErrorCode.MultipleGames : PgnErrorCode.UnexpectedToken,
                        _position, ply, "Only one game is accepted; unexpected content follows its result.");
                }

                document = PgnGame.FromGame(game, _tags, result);
                return true;
            }

            if (IsDigits(token))
            {
                if (pendingNumber.HasValue || !BigInteger.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out var number)
                    || number != game.FullmoveNumber)
                {
                    return Fail(PgnErrorCode.InvalidMoveNumber, start, ply, "The move number does not match the position.");
                }

                pendingNumber = start;
                SkipWhiteSpace();
                // PGN import permits zero or more periods, separated by whitespace.
                while (_position < _text.Length && _text[_position] == '.')
                {
                    ++_position;
                    SkipWhiteSpace();
                }

                continue;
            }

            if (_position < _text.Length && _text[_position] is '!' or '?')
            {
                return Fail(PgnErrorCode.UnsupportedAnnotation, _position, ply, "Move annotations are not supported.");
            }

            if (game.IsFinished)
            {
                return Fail(PgnErrorCode.MoveAfterGameEnd, start, ply, "No move is legal after the game has finished.");
            }

            if (game.MakeSanMove(token) == MoveResult.None)
            {
                return Fail(PgnErrorCode.InvalidSan, start, ply, "Expected a legal move in canonical SAN for this position.");
            }

            pendingNumber = null;
            ++ply;
        }
    }

    private bool ReadTag()
    {
        int tagStart = _position++;
        SkipWhiteSpace();
        int nameStart = _position;
        while (_position < _text.Length && (char.IsAsciiLetterOrDigit(_text[_position]) || _text[_position] == '_'))
        {
            ++_position;
        }

        int nameLength = _position - nameStart;
        if (nameLength is 0 or > 255 || !char.IsAsciiLetterOrDigit(_text[nameStart]))
        {
            return Fail(PgnErrorCode.InvalidTag, nameStart, null, "Invalid tag name.");
        }

        string name = _text.Slice(nameStart, nameLength).ToString();
        if (_tags.ContainsKey(name))
        {
            return Fail(PgnErrorCode.DuplicateTag, nameStart, null, "Tag names must be unique.");
        }

        SkipWhiteSpace();
        if (_position == _text.Length || _text[_position] != '"')
        {
            return Fail(PgnErrorCode.InvalidTag, _position, null, "Expected a quoted tag value.");
        }

        int valueStart = ++_position;
        Span<char> value = stackalloc char[255];
        int length = 0;
        while (_position < _text.Length && _text[_position] != '"')
        {
            int characterStart = _position;
            char c = _text[_position++];
            if (c == '\\')
            {
                if (_position == _text.Length || _text[_position] is not ('\\' or '"'))
                {
                    return Fail(PgnErrorCode.InvalidTag, characterStart, null, "Only quote and backslash escapes are supported.");
                }

                c = _text[_position++];
            }

            if (length == value.Length || char.IsControl(c) || c is '\u2028' or '\u2029')
            {
                return Fail(PgnErrorCode.InvalidTag, characterStart, null, "Tag values must contain at most 255 printable UTF-16 characters.");
            }

            value[length++] = c;
        }

        if (_position == _text.Length)
        {
            return Fail(PgnErrorCode.InvalidTag, _position, null, "Unterminated tag value.");
        }

        string decoded = value[..length].ToString();
        if (!PgnTags.IsValidValue(decoded))
        {
            return Fail(PgnErrorCode.InvalidTag, valueStart, null, "Invalid Unicode in tag value.");
        }

        ++_position;
        SkipWhiteSpace();
        if (_position == _text.Length || _text[_position] != ']')
        {
            return Fail(PgnErrorCode.InvalidTag, _position, null, "Expected the closing tag bracket.");
        }

        ++_position;
        _tags.Add(name, decoded);
        _tagOffsets.Add(name, tagStart);
        return true;
    }

    private bool RejectUnsupported(int ply)
    {
        char c = _text[_position];
        if (c is '{' or '}' or ';' or '$' or '(' or ')' or '!' or '?')
        {
            Fail(PgnErrorCode.UnsupportedAnnotation, _position, ply, "Comments, NAGs and variations are not supported.");
            return true;
        }

        if (c is '%' or '<' or '>')
        {
            Fail(PgnErrorCode.UnsupportedConstruct, _position, ply, "PGN escape lines and reserved constructs are not supported.");
            return true;
        }

        return false;
    }

    private void SkipWhiteSpace()
    {
        while (_position < _text.Length && _text[_position] is ' ' or '\t' or '\r' or '\n' or '\v')
        {
            ++_position;
        }
    }

    private static bool IsSymbolCharacter(char c) => char.IsAsciiLetterOrDigit(c) || c is '_' or '+' or '#' or '=' or ':' or '-' or '/';

    private static bool IsDigits(ReadOnlySpan<char> token)
    {
        foreach (char c in token)
        {
            if (!char.IsAsciiDigit(c))
            {
                return false;
            }
        }

        return true;
    }

    private bool Fail(PgnErrorCode code, int offset, int? ply, string message)
    {
        int line = 1;
        int column = 1;
        for (int i = 0; i < offset; ++i)
        {
            if (_text[i] == '\r')
            {
                if (i + 1 < offset && _text[i + 1] == '\n')
                {
                    ++i;
                }

                ++line;
                column = 1;
            }
            else if (_text[i] == '\n')
            {
                ++line;
                column = 1;
            }
            else
            {
                ++column;
            }
        }

        _error = new PgnDiagnostic(code, offset, line, column, ply, message);
        return false;
    }
}
