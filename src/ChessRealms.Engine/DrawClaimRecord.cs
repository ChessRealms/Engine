namespace ChessRealms.Engine;

/// <summary>A successful draw claim, optionally based on a legal move that was not played.</summary>
/// <param name="Reason">The claimed threefold-repetition or fifty-move rule.</param>
/// <param name="IntendedMove">The unplayed intended move, or null for a claim on the current position.</param>
public sealed record DrawClaimRecord(DrawClaim Reason, CoordinateMove? IntendedMove = null);
