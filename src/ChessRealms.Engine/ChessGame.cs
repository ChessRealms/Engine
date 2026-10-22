using ChessRealms.Engine.Core.Constants;
using ChessRealms.Engine.Core.Math;
using ChessRealms.Engine.Core.Movements;
using ChessRealms.Engine.Core.Types;
using ChessRealms.Engine.Json;
using ChessRealms.Engine.Parsing;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;

namespace ChessRealms.Engine;

/// <summary>A mutable game with exclusive history ownership. Use Clone for independent analysis.</summary>
public sealed partial class ChessGame
{
    /// <summary>The FEN for the standard starting position.</summary>
    public const string StartingFen = FenStrings.StartPosition;

    private Position position;
    private readonly string initialFen;
    private readonly List<RecordedMove> history = [];
    private readonly List<Position> undo = [];
    private RepetitionKey currentRepetitionKey;
    private readonly Dictionary<RepetitionKey, int> repetitions;

    public PieceColor CurrentColor => position.color.ToPublicColor();
    public PieceColor OpponentColor => Colors.Mirror(position.color).ToPublicColor();
    internal Position Position => position;
    internal string InitialFen => initialFen;
    public BigInteger HalfmoveClock => position.halfMoveClock;
    public BigInteger FullmoveNumber => position.fullMoveCount;
    public GameOutcome Outcome { get; private set; } = GameOutcome.Ongoing;

    /// <summary>The unplayed move of a successful intended-move draw claim, or null otherwise.</summary>
    public CoordinateMove? DrawIntendedMove { get; private set; }

    public bool IsFinished => Outcome.Result != GameResult.Ongoing;
    public bool IsInCheck => position.IsKingChecked();
    public GameState State
    {
        get
        {
            if (IsFinished)
            {
                return GameState.Finished;
            }

            return IsInCheck ? GameState.Check : GameState.Active;
        }
    }

    public int RepetitionCount => repetitions[currentRepetitionKey];

    /// <summary>The number of recorded half-moves since the initial position, excluding undone moves.</summary>
    public int PlyCount => history.Count;

    /// <summary>An independent read-only snapshot of moves, their before/after FEN and move flags.</summary>
    public IReadOnlyList<MoveHistoryEntry> History => CopyHistory();
    public DrawClaim AvailableDrawClaims => IsFinished
        ? DrawClaim.None
        : CalculateDrawClaims(position, RepetitionCount);

    public ChessGame() : this(Position.CreateDefault())
    {
    }

    internal ChessGame(Position position)
    {
        if (!PositionValidation.IsValid(position))
        {
            throw new ArgumentException("Invalid standard chess position.", nameof(position));
        }

        this.position = position;
        initialFen = FenStrings.Format(position);
        Span<int> legalMoves = stackalloc int[MoveGen.MaxMoves];
        int legalMoveCount = LegalMoves.Write(position, legalMoves);
        currentRepetitionKey = RepetitionKey.Create(position, legalMoves[..legalMoveCount]);
        repetitions = new Dictionary<RepetitionKey, int> { [currentRepetitionKey] = 1 };
        Outcome = EvaluateOutcome(position, legalMoveCount, 1);
    }

    private ChessGame(ChessGame original)
    {
        position = original.position;
        initialFen = original.initialFen;
        Outcome = original.Outcome;
        DrawIntendedMove = original.DrawIntendedMove;
        history.AddRange(original.history);
        undo.AddRange(original.undo);
        currentRepetitionKey = original.currentRepetitionKey;
        repetitions = new Dictionary<RepetitionKey, int>(original.repetitions);
    }

    public ChessGame Clone() => new(this);

    /// <summary>Gets the current position's canonical FEN, formatting and caching it on first request.</summary>
    public string ToFen() => GetFenAtPly(history.Count);

    /// <summary>Writes the current position as canonical FEN without creating a string.</summary>
    /// <remarks>Does not change the game. If the destination is too small, returns false with zero
    /// characters written and leaves the destination unchanged. Move counters have arbitrary precision,
    /// so there is no fixed maximum FEN length. Also works after the game finishes.</remarks>
    public bool TryFormatFen(Span<char> destination, out int charsWritten)
        => FenStrings.TryFormatUnchecked(position, destination, out charsWritten);

    /// <summary>Exports an independent snapshot of the starting position, current move line and draw claim.</summary>
    public GameRecord ExportRecord() => GameRecord.CreateFromGame(this);

    internal CoordinateMove[] CopyRecordedMoves()
    {
        if (history.Count == 0)
        {
            return [];
        }

        var moves = new CoordinateMove[history.Count];
        for (int i = 0; i < history.Count; ++i)
        {
            moves[i] = history[i].Move;
        }

        return moves;
    }

    /// <summary>Restores an independent game by replaying the record's moves and applying its draw claim.</summary>
    /// <exception cref="ArgumentNullException">The record is null.</exception>
    /// <exception cref="FormatException">The initial FEN, a recorded move or the draw claim is invalid.</exception>
    public static ChessGame FromRecord(GameRecord gameRecord) => GameRecord.RestoreGame(gameRecord);

    /// <summary>Restores an independent game from versioned game-record JSON, validating and replaying it once.</summary>
    /// <remarks>Accepts the format written by <see cref="GameRecord.Serialize"/>. Rebuilds history,
    /// repetition counts, outcome and undo, including a draw claim's unplayed intended move.</remarks>
    /// <exception cref="ArgumentNullException">The JSON string is null.</exception>
    /// <exception cref="System.Text.Json.JsonException">The JSON is invalid, its schema is unsupported,
    /// or the game cannot be restored. Replay errors retain their original FormatException as the inner exception.</exception>
    public static ChessGame FromRecordJson(string json) => GameRecordJsonSerializer.DeserializeGame(json);

    /// <summary>Copies the board in a1-to-h8 order.</summary>
    public void CopyBoardTo(Span<ChessPiece> destination)
    {
        if (destination.Length < 64)
        {
            throw new ArgumentException("Board requires 64 squares.", nameof(destination));
        }

        for (int i = 0; i < 64; i++)
        {
            var piece = position.GetPieceAt(i, Colors.White);
            if (!Piece.IsValid(piece))
            {
                piece = position.GetPieceAt(i, Colors.Black);
            }

            destination[i] = Piece.IsValid(piece)
                ? new(piece.Color.ToPublicColor(), piece.Value.ToPublicPiece())
                : ChessPiece.Empty;
        }
    }

    /// <summary>Gets the piece on a square, or <see cref="ChessPiece.Empty"/> if it is vacant.</summary>
    public ChessPiece GetPiece(Square square)
    {
        if (!square.IsValid)
        {
            throw new ArgumentException("A valid square is required.", nameof(square));
        }

        var piece = position.GetPieceAt(square.Index, Colors.White);
        if (!Piece.IsValid(piece))
        {
            piece = position.GetPieceAt(square.Index, Colors.Black);
        }

        return Piece.IsValid(piece)
            ? new(piece.Color.ToPublicColor(), piece.Value.ToPublicPiece())
            : ChessPiece.Empty;
    }

    /// <summary>Snapshot of playable moves; empty after completion. Does not mutate the game.</summary>
    public IReadOnlyList<CoordinateMove> GetLegalMoves()
    {
        if (IsFinished)
        {
            return Array.Empty<CoordinateMove>();
        }

        Span<int> legalMoves = stackalloc int[MoveGen.MaxMoves];
        int legalMoveCount = LegalMoves.Write(position, legalMoves);
        var moves = new CoordinateMove[legalMoveCount];
        for (int i = 0; i < legalMoveCount; ++i)
        {
            moves[i] = LegalMoves.ToCoordinateMove(legalMoves[i]);
        }

        return Array.AsReadOnly(moves);
    }

    // Outcome evaluation already finishes games with no legal moves.
    public bool HasLegalMoves => !IsFinished;

    public MoveResult MakeMove(CoordinateMove move)
    {
        if (IsFinished || !TryFindMove(move, out int encodedMove))
        {
            return MoveResult.None;
        }

        return ApplyLegalMove(move, encodedMove);
    }

    private MoveResult ApplyLegalMove(CoordinateMove move, int encodedMove)
    {
        var nextPosition = position;
        MoveDriver.MakeMove(ref nextPosition, encodedMove);
        nextPosition.SwitchColor();

        Span<int> replies = stackalloc int[MoveGen.MaxMoves];
        int replyCount = LegalMoves.Write(nextPosition, replies);
        RepetitionKey key = RepetitionKey.Create(nextPosition, replies[..replyCount]);
        int repetitionCount = repetitions.GetValueOrDefault(key) + 1;
        var outcome = EvaluateOutcome(nextPosition, replyCount, repetitionCount);

        MoveResult result = MoveResult.Move;
        if (BinaryMoveOps.DecodeCapture(encodedMove) != 0)
        {
            result |= MoveResult.Capture;
        }

        if (nextPosition.IsKingChecked())
        {
            result |= MoveResult.Check;
        }

        if (outcome.Reason == FinishReason.Checkmate)
        {
            result |= MoveResult.Checkmate;
        }

        if (outcome.Reason == FinishReason.Stalemate)
        {
            result |= MoveResult.Stalemate;
        }

        var entry = new RecordedMove(move, result);
        undo.Add(position);
        history.Add(entry);
        currentRepetitionKey = key;
        repetitions[key] = repetitionCount;
        position = nextPosition;
        Outcome = outcome;

        return result;
    }

    /// <summary>Undo the last successful move, also clearing a later draw claim.</summary>
    public bool UndoMove()
    {
        if (undo.Count == 0)
        {
            return false;
        }

        RepetitionKey key = currentRepetitionKey;
        if (--repetitions[key] == 0)
        {
            repetitions.Remove(key);
        }

        position = undo[^1];
        undo.RemoveAt(undo.Count - 1);
        history.RemoveAt(history.Count - 1);
        Span<int> legalMoves = stackalloc int[MoveGen.MaxMoves];
        int legalMoveCount = LegalMoves.Write(position, legalMoves);
        // Undo already retains the position and regenerates its legal moves.
        // Reuse them instead of storing another key for every ply.
        currentRepetitionKey = RepetitionKey.Create(position, legalMoves[..legalMoveCount]);
        Outcome = EvaluateOutcome(position, legalMoveCount, RepetitionCount);
        DrawIntendedMove = null;

        return true;
    }

    /// <summary>Claims available after an intended legal move, without executing it.</summary>
    public DrawClaim GetAvailableDrawClaims(CoordinateMove intendedMove)
    {
        if (IsFinished || !TryFindMove(intendedMove, out int encodedMove))
        {
            return DrawClaim.None;
        }

        var nextPosition = position;
        MoveDriver.MakeMove(ref nextPosition, encodedMove);
        nextPosition.SwitchColor();

        Span<int> legalMoves = stackalloc int[MoveGen.MaxMoves];
        int legalMoveCount = LegalMoves.Write(nextPosition, legalMoves);
        RepetitionKey key = RepetitionKey.Create(nextPosition, legalMoves[..legalMoveCount]);
        int repetitionCount = repetitions.GetValueOrDefault(key) + 1;
        return CalculateDrawClaims(nextPosition, repetitionCount);
    }

    /// <summary>A valid intended-move claim ends the game at its current board; the move is not played.</summary>
    public bool ClaimDraw(DrawClaim reason, CoordinateMove? intendedMove = null)
    {
        if (reason is not (DrawClaim.ThreefoldRepetition or DrawClaim.FiftyMoveRule))
        {
            return false;
        }

        DrawClaim available = intendedMove is { } move
            ? GetAvailableDrawClaims(move)
            : AvailableDrawClaims;

        if ((available & reason) == 0)
        {
            return false;
        }

        FinishReason finishReason = reason == DrawClaim.ThreefoldRepetition
            ? FinishReason.ThreefoldRepetition
            : FinishReason.FiftyMoveRule;
        Outcome = Draw(finishReason);
        DrawIntendedMove = intendedMove;

        return true;
    }

    private bool TryFindMove(CoordinateMove move, out int encodedMove)
    {
        encodedMove = 0;
        if (!move.IsValid)
        {
            return false;
        }

        Span<int> legalMoves = stackalloc int[MoveGen.MaxMoves];
        int legalMoveCount = LegalMoves.Write(position, legalMoves);
        return LegalMoves.TryFind(move, legalMoves[..legalMoveCount], out encodedMove);
    }

    private static DrawClaim CalculateDrawClaims(Position position, int repetitionCount) =>
        (repetitionCount >= 3 ? DrawClaim.ThreefoldRepetition : DrawClaim.None)
        | (position.halfMoveClock >= 100 ? DrawClaim.FiftyMoveRule : DrawClaim.None);

    private static GameOutcome EvaluateOutcome(Position position, int legalMoveCount, int repetitionCount)
    {
        if (legalMoveCount == 0)
        {
            if (!position.IsKingChecked())
            {
                return Draw(FinishReason.Stalemate);
            }

            PieceColor winner = Colors.Mirror(position.color).ToPublicColor();
            GameResult result = winner == PieceColor.White ? GameResult.WhiteWin : GameResult.BlackWin;
            return new(result, winner, FinishReason.Checkmate);
        }

        if (IsBasicDeadPosition(position))
        {
            return Draw(FinishReason.DeadPosition);
        }

        if (repetitionCount >= 5)
        {
            return Draw(FinishReason.FivefoldRepetition);
        }

        if (position.halfMoveClock >= 150)
        {
            return Draw(FinishReason.SeventyFiveMoveRule);
        }

        return GameOutcome.Ongoing;
    }

    private static GameOutcome Draw(FinishReason reason) => new(GameResult.Draw, PieceColor.None, reason);

    private static bool IsBasicDeadPosition(Position position)
    {
        ulong pawns = position.pieceBBs[BitboardIndicies.BPawn] | position.pieceBBs[BitboardIndicies.WPawn];
        ulong rooks = position.pieceBBs[BitboardIndicies.BRook] | position.pieceBBs[BitboardIndicies.WRook];
        ulong queens = position.pieceBBs[BitboardIndicies.BQueen] | position.pieceBBs[BitboardIndicies.WQueen];
        if ((pawns | rooks | queens) != 0)
        {
            return false;
        }

        ulong knights = position.pieceBBs[BitboardIndicies.BKnight] | position.pieceBBs[BitboardIndicies.WKnight];
        ulong bishops = position.pieceBBs[BitboardIndicies.BBishop] | position.pieceBBs[BitboardIndicies.WBishop];
        if (BitOperations.PopCount(knights | bishops) <= 1)
        {
            return true;
        }
        // Bishops alone, all on one square color. Never infer deadness from inability to force mate.
        return knights == 0
            && ((bishops & SquareMapping.DARK_SQUARES) == 0 || (bishops & SquareMapping.LIGHT_SQUARES) == 0);
    }

    public static bool TryCreateFromFen(string? fen, [NotNullWhen(true)] out ChessGame? chessGame)
    {
        chessGame = FenStrings.TryParse(fen, out var position) ? new ChessGame(position) : null;
        return chessGame is not null;
    }

    /// <summary>Creates a game from a valid six-field Forsyth-Edwards Notation string.</summary>
    /// <exception cref="FormatException">The FEN does not describe a valid standard-chess position.</exception>
    public static ChessGame FromFen(string fen)
        => TryCreateFromFen(fen, out var game) ? game : throw new FormatException("Invalid standard-chess FEN.");
}
