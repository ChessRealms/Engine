namespace ChessRealms.Engine;

[Flags]
public enum MoveResult
{
    None = 0,
    Move = 1,
    Capture = 2,
    Check = 4,
    Checkmate = 8,
    Stalemate = 16
}
