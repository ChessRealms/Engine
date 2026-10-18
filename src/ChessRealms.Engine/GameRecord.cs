using ChessRealms.Engine.Core.Types;
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

    internal GameRecord(string fen, IReadOnlyList<CoordinateMove> moves, DrawClaimRecord? drawClaim)
    {
        ArgumentNullException.ThrowIfNull(fen);
        ArgumentNullException.ThrowIfNull(moves);

        InitialFen = fen;
        Moves = Array.AsReadOnly(moves.ToArray());
        DrawClaim = drawClaim;
    }

    internal static GameRecord CreateFromGame(ChessGame chessGame)
    {
        ArgumentNullException.ThrowIfNull(chessGame);

        var fen = chessGame.InitialFen;
        var moves = chessGame.History.Select(x => x.Move).ToArray();
        DrawClaimRecord? drawClaim = chessGame.Outcome.Reason switch
        {
            FinishReason.ThreefoldRepetition => new(ChessRealms.Engine.DrawClaim.ThreefoldRepetition,
                chessGame.DrawIntendedMove),
            FinishReason.FiftyMoveRule => new(ChessRealms.Engine.DrawClaim.FiftyMoveRule,
                chessGame.DrawIntendedMove),
            _ => null
        };

        return new GameRecord(fen, moves, drawClaim);
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
