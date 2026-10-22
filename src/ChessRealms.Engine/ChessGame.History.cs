using ChessRealms.Engine.Core.Types;
using ChessRealms.Engine.Parsing;

namespace ChessRealms.Engine;

public sealed partial class ChessGame
{
    // Each state already exists in undo or position. Cache text only after a string query.
    // Copying this value into a clone shares immutable strings, never mutable cache ownership.
    private readonly record struct RecordedMove(CoordinateMove Move, MoveResult Result, string? CachedFen = null);

    /// <summary>Gets canonical FEN after the specified number of recorded half-moves.</summary>
    /// <remarks>Zero selects the initial position, including a custom starting FEN; PlyCount selects
    /// the current position. The index follows the current line after undo. Formats only the selected
    /// position and caches its string. Does not change the game's position, history or outcome.</remarks>
    /// <exception cref="ArgumentOutOfRangeException">The ply is negative or greater than PlyCount.</exception>
    public string GetFenAtPly(int ply)
    {
        if ((uint)ply > (uint)history.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(ply), ply, "The ply must be between zero and PlyCount.");
        }

        return ply == 0 ? initialFen : GetOrCreateFenAfter(ply - 1);
    }

    /// <summary>Writes canonical FEN after the specified number of recorded half-moves without creating a string.</summary>
    /// <remarks>Zero selects the initial position; PlyCount selects the current position. Returns false
    /// with zero characters written and leaves the destination unchanged for an out-of-range ply or
    /// insufficient space. Does not populate string caches or change the game. Move counters have
    /// arbitrary precision, so there is no fixed maximum FEN length.</remarks>
    public bool TryFormatFenAtPly(int ply, Span<char> destination, out int charsWritten)
    {
        charsWritten = 0;
        return (uint)ply <= (uint)history.Count
            && FenFormatter.TryFormatWithoutValidation(GetPositionAtPly(ply), destination, out charsWritten);
    }

    // The caller has checked the range. undo[i] is the position before move i.
    private Position GetPositionAtPly(int ply) => ply == history.Count ? position : undo[ply];

    private string GetOrCreateFenAfter(int moveIndex)
    {
        var entry = history[moveIndex];
        if (entry.CachedFen is { } fen)
        {
            return fen;
        }

        fen = FenFormatter.FormatWithoutValidation(GetPositionAtPly(moveIndex + 1));
        history[moveIndex] = entry with { CachedFen = fen };
        return fen;
    }

    private IReadOnlyList<MoveHistoryEntry> CopyHistory()
    {
        if (history.Count == 0)
        {
            return Array.AsReadOnly(Array.Empty<MoveHistoryEntry>());
        }

        var snapshot = new MoveHistoryEntry[history.Count];
        string before = initialFen;
        for (int i = 0; i < snapshot.Length; i++)
        {
            var entry = history[i];
            string after = entry.CachedFen ?? GetOrCreateFenAfter(i);
            snapshot[i] = new MoveHistoryEntry(entry.Move, before, after, entry.Result);
            before = after;
        }

        return Array.AsReadOnly(snapshot);
    }
}
