namespace ChessRealms.Engine;

public enum FinishReason
{
    None,
    Checkmate,
    Stalemate,
    DeadPosition,
    ThreefoldRepetition,
    FiftyMoveRule,
    FivefoldRepetition,
    SeventyFiveMoveRule
}
