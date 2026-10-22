using ChessRealms.Engine.Parsing;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace ChessRealms.Engine;

/// <summary>An immutable single-game PGN mainline, with tags and a declared result.</summary>
public sealed class PgnGame
{
    public string InitialFen { get; }
    public IReadOnlyList<CoordinateMove> Moves { get; }
    public IReadOnlyDictionary<string, string> Tags { get; }
    public PgnResult Result { get; }

    private PgnGame(ChessGame game, Dictionary<string, string> tags, PgnResult result)
    {
        InitialFen = game.InitialFen;
        Moves = Array.AsReadOnly(game.CopyRecordedMoves());
        Result = result;
        Tags = new ReadOnlyDictionary<string, string>(PgnTags.Normalize(tags, InitialFen, result));
    }

    /// <summary>Snapshots a game's current line. Supplied tags are copied; missing roster tags get defaults.</summary>
    /// <remarks>Result defaults to the supplied Result tag, or the game's outcome. An explicit result must
    /// agree with a supplied Result tag and any finished engine outcome. Declared results do not modify the game.
    /// PGN does not preserve the reason or intended move of a draw claim; use GameRecord for that purpose.</remarks>
    /// <exception cref="ArgumentNullException">The game is null.</exception>
    /// <exception cref="ArgumentException">A tag, result, or starting-position declaration is invalid or conflicting.</exception>
    public static PgnGame FromGame(ChessGame game, IEnumerable<KeyValuePair<string, string>>? tags = null,
        PgnResult? result = null)
    {
        ArgumentNullException.ThrowIfNull(game);
        var copy = new Dictionary<string, string>(StringComparer.Ordinal);
        if (tags is not null)
        {
            foreach (var pair in tags)
            {
                if (!PgnTags.IsValidName(pair.Key) || !PgnTags.IsValidValue(pair.Value)
                    || !copy.TryAdd(pair.Key, pair.Value))
                {
                    throw new ArgumentException("Tag names and values must be valid and names must be unique.", nameof(tags));
                }
            }
        }

        if (copy.TryGetValue("Variant", out var variant) && variant != "Standard")
        {
            throw new ArgumentException("Only standard chess PGN is supported.", nameof(tags));
        }

        if (!PgnTags.TryReadSetup(copy, out var declaredFen))
        {
            throw new ArgumentException("SetUp and FEN must describe a consistent starting position.", nameof(tags));
        }

        if (declaredFen is not null && (!ChessGame.TryCreateFromFen(declaredFen, out var declaredGame)
            || declaredGame.ToFen() != game.InitialFen))
        {
            throw new ArgumentException("The FEN tag must match the game's initial position.", nameof(tags));
        }

        if (copy.TryGetValue("SetUp", out var setup) && setup == "0" && game.InitialFen != ChessGame.StartingFen)
        {
            throw new ArgumentException("SetUp 0 conflicts with the game's initial position.", nameof(tags));
        }

        if (!PgnTags.IsValidValue(game.InitialFen))
        {
            throw new ArgumentException("The initial FEN exceeds the PGN tag-value limit.", nameof(game));
        }

        PgnResult selected = result ?? PgnTags.FromOutcome(game.Outcome.Result);
        if (copy.TryGetValue("Result", out var marker))
        {
            if (!PgnTags.TryReadResult(marker, out var taggedResult) || (result.HasValue && result.Value != taggedResult))
            {
                throw new ArgumentException("The Result tag is invalid or conflicts with the supplied result.", nameof(tags));
            }

            selected = taggedResult;
        }

        if (!Enum.IsDefined(selected) || (game.IsFinished && selected != PgnTags.FromOutcome(game.Outcome.Result)))
        {
            throw new ArgumentException("The declared result is invalid or contradicts the finished game.", nameof(result));
        }

        return new PgnGame(game, copy, selected);
    }

    /// <summary>Replays the line into an independent game. Does not infer resignation, timeout or draw claims
    /// from the declared result; an ongoing engine outcome may accompany a decisive PGN result.</summary>
    public ChessGame ToGame() => ChessGame.FromRecord(new GameRecord(InitialFen, Moves, null));

    /// <summary>Exports ordered tags and canonical SAN with LF line endings.</summary>
    public string ToPgn()
    {
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        WriteTo(writer);
        return writer.ToString();
    }

    /// <summary>Writes PGN without closing the supplied writer. Writer failures propagate.</summary>
    public void WriteTo(TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        PgnWriter.Write(this, writer);
    }

    /// <summary>Imports exactly one mainline PGN game with canonical SAN.</summary>
    /// <exception cref="PgnParseException">The PGN is invalid or uses unsupported annotations or variants.</exception>
    public static PgnGame Parse(ReadOnlySpan<char> text)
        => TryParse(text, out var game, out var diagnostic) ? game : throw new PgnParseException(diagnostic);

    /// <summary>Imports a PGN document and returns the game built during validation, replaying the mainline once.</summary>
    /// <remarks>The game includes history, counters, repetition and undo. The returned document is an immutable
    /// snapshot and is unaffected by later game changes. Its declared result does not apply a new engine ending;
    /// an ongoing game can accompany a decisive PGN result, as with <see cref="ToGame"/>.</remarks>
    /// <exception cref="PgnParseException">The PGN is invalid or uses unsupported annotations or variants.</exception>
    public static PgnGame Parse(ReadOnlySpan<char> text, out ChessGame game)
    {
        if (!TryParse(text, out var document, out var parsedGame, out var diagnostic))
        {
            throw new PgnParseException(diagnostic);
        }

        game = parsedGame;
        return document;
    }

    /// <summary>Imports atomically. On failure returns no game and a diagnostic; on success the diagnostic is null.</summary>
    public static bool TryParse(ReadOnlySpan<char> text, [NotNullWhen(true)] out PgnGame? game,
        [NotNullWhen(false)] out PgnDiagnostic? diagnostic)
        => PgnReader.TryRead(text, out game, out _, out diagnostic);

    /// <summary>Imports a document and its game in one mainline replay. On failure both outputs are null;
    /// on success both are non-null and the diagnostic is null.</summary>
    /// <remarks>The game is exposed only after the entire input is validated. Later game changes do not
    /// affect the document. The declared PGN result remains separate from the engine outcome.</remarks>
    public static bool TryParse(ReadOnlySpan<char> text, [NotNullWhen(true)] out PgnGame? document,
        [NotNullWhen(true)] out ChessGame? game, [NotNullWhen(false)] out PgnDiagnostic? diagnostic)
        => PgnReader.TryRead(text, out document, out game, out diagnostic);
}
