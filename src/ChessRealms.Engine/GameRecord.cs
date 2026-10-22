using ChessRealms.Engine.Core.Types;
using ChessRealms.Engine.Json;
using ChessRealms.Engine.Parsing;

namespace ChessRealms.Engine;

/// <summary>An immutable snapshot of a game's starting position, current move line and draw claim.</summary>
public sealed class GameRecord
{
    /// <summary>The version of the game record schema.</summary>
    public const int SchemaVersion = 1;

    /// <summary>The position from which the recorded moves were played.</summary>
    public string InitialFen { get; }

    /// <summary>A read-only snapshot of the successfully played moves, excluding undone moves.</summary>
    public IReadOnlyList<CoordinateMove> Moves { get; }

    /// <summary>The successful draw claim, or null if no claim ended the game.</summary>
    public DrawClaimRecord? DrawClaim { get; }

    /// <summary>Serializes a game record to versioned JSON with coordinate moves written as strings.</summary>
    /// <exception cref="ArgumentNullException">The record is null.</exception>
    public static string Serialize(GameRecord record) => GameRecordJsonSerializer.Serialize(record);

    /// <summary>Deserializes a game record and validates it by replaying its moves and draw claim.</summary>
    /// <remarks>Use <see cref="ChessGame.FromRecordJson"/> when a playable game is needed directly.</remarks>
    /// <exception cref="ArgumentNullException">The JSON string is null.</exception>
    /// <exception cref="System.Text.Json.JsonException">The JSON is invalid, its schema is unsupported,
    /// or the game cannot be restored.</exception>
    public static GameRecord Deserialize(string json) => GameRecordJsonSerializer.Deserialize(json);

    internal GameRecord(string fen, IReadOnlyList<CoordinateMove> moves, DrawClaimRecord? drawClaim)
    {
        ArgumentNullException.ThrowIfNull(fen);
        ArgumentNullException.ThrowIfNull(moves);

        InitialFen = fen;
        Moves = Array.AsReadOnly(moves.ToArray());
        DrawClaim = drawClaim;
    }

    private GameRecord(ChessGame game, DrawClaimRecord? drawClaim)
    {
        InitialFen = game.InitialFen;
        Moves = Array.AsReadOnly(game.CopyRecordedMoves());
        DrawClaim = drawClaim;
    }

    internal static GameRecord CreateFromGame(ChessGame chessGame)
    {
        ArgumentNullException.ThrowIfNull(chessGame);

        DrawClaimRecord? drawClaim = chessGame.Outcome.Reason switch
        {
            FinishReason.ThreefoldRepetition => new(ChessRealms.Engine.DrawClaim.ThreefoldRepetition,
                chessGame.DrawIntendedMove),
            FinishReason.FiftyMoveRule => new(ChessRealms.Engine.DrawClaim.FiftyMoveRule,
                chessGame.DrawIntendedMove),
            _ => null
        };

        return new GameRecord(chessGame, drawClaim);
    }

    internal static ChessGame RestoreGame(GameRecord gameRecord)
    {
        ArgumentNullException.ThrowIfNull(gameRecord);

        if (!FenStrings.TryParse(gameRecord.InitialFen, out Position position))
        {
            throw new FormatException("The game record contains an invalid initial FEN.");
        }

        var game = new ChessGame(position);

        for (int i = 0; i < gameRecord.Moves.Count; ++i)
        {
            CoordinateMove move = gameRecord.Moves[i];
            MoveResult result = game.MakeMove(move);

            if (result == MoveResult.None)
            {
                throw new FormatException($"The game record contains an unplayable move at index {i}: '{move}'.");
            }
        }

        if (gameRecord.DrawClaim is { } claim && !game.ClaimDraw(claim.Reason, claim.IntendedMove))
        {
            throw new FormatException("The game record contains an invalid or unavailable draw claim.");
        }

        return game;
    }
}
