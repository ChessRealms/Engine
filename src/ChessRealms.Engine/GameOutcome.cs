using ChessRealms.Engine.Json;
using System.Text.Json.Serialization;

namespace ChessRealms.Engine;

public enum GameState
{
    Active,
    Check,
    Finished
}

public enum GameResult
{
    Ongoing,
    WhiteWin,
    BlackWin,
    Draw
}

[Flags]
[JsonConverter(typeof(DrawClaimConverter))]
public enum DrawClaim
{
    None = 0,
    ThreefoldRepetition = 1,
    FiftyMoveRule = 2
}

public readonly record struct GameOutcome(GameResult Result, PieceColor Winner, FinishReason Reason)
{
    public static GameOutcome Ongoing => new(GameResult.Ongoing, PieceColor.None, FinishReason.None);
}

public readonly record struct MoveHistoryEntry(CoordinateMove Move, string FenBefore, string FenAfter, MoveResult Result);
