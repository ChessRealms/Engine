namespace ChessRealms.Engine;

/// <summary>The declared PGN result, independent of an engine's board-derived outcome.</summary>
public enum PgnResult
{
    /// <summary>The marker *, covering ongoing games and unavailable results.</summary>
    Unknown,
    WhiteWin,
    BlackWin,
    Draw
}
