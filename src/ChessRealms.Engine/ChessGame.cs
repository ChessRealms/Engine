using ChessRealms.Engine.Core.Constants;
using ChessRealms.Engine.Core.Math;
using ChessRealms.Engine.Core.Movements;
using ChessRealms.Engine.Core.Types;
using ChessRealms.Engine.Parsing;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;

namespace ChessRealms.Engine;

/// <summary>A mutable game with exclusive history ownership. Use Clone for independent analysis.</summary>
public sealed class ChessGame
{
    /// <summary>The FEN for the standard starting position.</summary>
    public const string StartingFen = FenStrings.StartPosition;

    private Position position;
    private readonly string initialFen;
    private readonly List<MoveHistoryEntry> history = [];
    private readonly List<Position> undo = [];
    private readonly List<string> keys = [];
    private readonly Dictionary<string, int> repetitions = new(StringComparer.Ordinal);

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

    public int RepetitionCount => repetitions[keys[^1]];
    public IReadOnlyList<MoveHistoryEntry> History => Array.AsReadOnly(history.ToArray());
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
        int legalMoveCount = WriteLegalMoves(position, legalMoves);
        string key = CreateRepetitionKey(position, legalMoves[..legalMoveCount]);
        keys.Add(key);
        repetitions.Add(key, 1);
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
        keys.AddRange(original.keys);
        foreach (var pair in original.repetitions)
        {
            repetitions.Add(pair.Key, pair.Value);
        }
    }

    public ChessGame Clone() => new(this);
    public string ToFen() => history.Count == 0 ? initialFen : history[^1].FenAfter;

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
        int legalMoveCount = WriteLegalMoves(position, legalMoves);
        var moves = new CoordinateMove[legalMoveCount];
        for (int i = 0; i < legalMoveCount; ++i)
        {
            moves[i] = ToPublicMove(legalMoves[i]);
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

        var nextPosition = position;
        MoveDriver.MakeMove(ref nextPosition, encodedMove);
        nextPosition.SwitchColor();

        Span<int> replies = stackalloc int[MoveGen.MaxMoves];
        int replyCount = WriteLegalMoves(nextPosition, replies);
        string key = CreateRepetitionKey(nextPosition, replies[..replyCount]);
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

        var entry = new MoveHistoryEntry(move, ToFen(), FenStrings.FormatUnchecked(nextPosition), result);
        undo.Add(position);
        history.Add(entry);
        keys.Add(key);
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

        string key = keys[^1];
        if (--repetitions[key] == 0)
        {
            repetitions.Remove(key);
        }

        keys.RemoveAt(keys.Count - 1);
        position = undo[^1];
        undo.RemoveAt(undo.Count - 1);
        history.RemoveAt(history.Count - 1);
        Span<int> legalMoves = stackalloc int[MoveGen.MaxMoves];
        int legalMoveCount = WriteLegalMoves(position, legalMoves);
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
        int legalMoveCount = WriteLegalMoves(nextPosition, legalMoves);
        string key = CreateRepetitionKey(nextPosition, legalMoves[..legalMoveCount]);
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
        int legalMoveCount = WriteLegalMoves(position, legalMoves);
        foreach (int candidate in legalMoves[..legalMoveCount])
        {
            if (ToPublicMove(candidate) != move)
            {
                continue;
            }

            encodedMove = candidate;
            return true;
        }

        return false;
    }

    private static CoordinateMove ToPublicMove(int encodedMove) => new(
        new Square(BinaryMoveOps.DecodeSrc(encodedMove)),
        new Square(BinaryMoveOps.DecodeTrg(encodedMove)),
        BinaryMoveOps.DecodePromotion(encodedMove).ToPublicPromotion());

    private static int WriteLegalMoves(Position position, Span<int> moves)
    {
        int written = MoveGen.WriteMoves(ref position, position.color, moves);
        int legalMoveCount = 0;
        for (int i = 0; i < written; i++)
        {
            var nextPosition = position;
            // Move legality is independent of FEN counters.
            MoveDriver.MakeMove(ref nextPosition, moves[i], updateCounters: false);
            if (!nextPosition.IsKingChecked(position.color))
            {
                moves[legalMoveCount++] = moves[i];
            }
        }

        return legalMoveCount;
    }

    private static string CreateRepetitionKey(Position position, ReadOnlySpan<int> legalMoves)
    {
        bool hasLegalEnPassant = false;
        foreach (int move in legalMoves)
        {
            if (BinaryMoveOps.DecodeEnpassant(move) != 0)
            {
                hasLegalEnPassant = true;
                break;
            }
        }

        if (!hasLegalEnPassant)
        {
            position.enpassant = Squares.Empty;
        }

        return FenStrings.FormatPositionFields(position);
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
