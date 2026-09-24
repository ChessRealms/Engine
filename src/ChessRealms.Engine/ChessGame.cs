using ChessRealms.Engine.Core.Constants;
using ChessRealms.Engine.Core.Math;
using ChessRealms.Engine.Core.Movements;
using ChessRealms.Engine.Core.Types;
using ChessRealms.Engine.Parsing;
using System.Numerics;

namespace ChessRealms.Engine;

/// <summary>A mutable game with exclusive history ownership. Use Clone for independent analysis.</summary>
public sealed class ChessGame
{
    /// <summary>The FEN for the standard starting position.</summary>
    public const string StartingFen = FenStrings.StartPosition;

    private Position position;
    private readonly List<MoveHistoryEntry> history = [];
    private readonly List<Position> undo = [];
    private readonly List<string> keys = [];
    private readonly Dictionary<string, int> repetitions = new(StringComparer.Ordinal);

    public PieceColor CurrentColor => position.color.ToPublicColor();
    public PieceColor OpponentColor => Colors.Mirror(position.color).ToPublicColor();
    internal Position Position => position;
    public BigInteger HalfmoveClock => position.halfMoveClock;
    public BigInteger FullmoveNumber => position.fullMoveCount;
    public GameOutcome Outcome { get; private set; } = GameOutcome.Ongoing;
    public bool IsFinished => Outcome.Result != GameResult.Ongoing;
    public bool IsInCheck => position.IsKingChecked();
    public GameState State => IsFinished ? GameState.Finished : IsInCheck ? GameState.Check : GameState.Active;
    public int RepetitionCount => repetitions[keys[^1]];
    public IReadOnlyList<MoveHistoryEntry> History => Array.AsReadOnly(history.ToArray());
    public DrawClaim AvailableDrawClaims => IsFinished ? DrawClaim.None : Claims(position, RepetitionCount);

    public ChessGame() : this(Position.CreateDefault()) { }

    internal ChessGame(Position position)
    {
        if (!PositionValidation.IsValid(position)) throw new ArgumentException("Invalid standard chess position.", nameof(position));
        this.position = position;
        var moves = LegalMoves(position);
        string key = RepetitionKey(position, moves);
        keys.Add(key);
        repetitions.Add(key, 1);
        Outcome = Evaluate(position, moves.Count, 1);
    }

    private ChessGame(ChessGame original)
    {
        position = original.position;
        Outcome = original.Outcome;
        history.AddRange(original.history);
        undo.AddRange(original.undo);
        keys.AddRange(original.keys);
        foreach (var pair in original.repetitions) repetitions.Add(pair.Key, pair.Value);
    }

    public ChessGame Clone() => new(this);
    public string ToFen() => FenStrings.FormatUnchecked(position);

    /// <summary>Copies the board in a1-to-h8 order.</summary>
    public void CopyBoardTo(Span<ChessPiece> destination)
    {
        if (destination.Length < 64) throw new ArgumentException("Board requires 64 squares.", nameof(destination));
        for (int i = 0; i < 64; i++)
        {
            var piece = position.GetPieceAt(i, Colors.White);
            if (!Piece.IsValid(piece)) piece = position.GetPieceAt(i, Colors.Black);
            destination[i] = Piece.IsValid(piece)
                ? new(piece.Color.ToPublicColor(), piece.Value.ToPublicPiece())
                : ChessPiece.Empty;
        }
    }

    /// <summary>Gets the piece on a square, or <see cref="ChessPiece.Empty"/> if it is vacant.</summary>
    public ChessPiece GetPiece(Square square)
    {
        if (!square.IsValid) throw new ArgumentException("A valid square is required.", nameof(square));
        var piece = position.GetPieceAt(square.Index, Colors.White);
        if (!Piece.IsValid(piece)) piece = position.GetPieceAt(square.Index, Colors.Black);
        return Piece.IsValid(piece)
            ? new(piece.Color.ToPublicColor(), piece.Value.ToPublicPiece())
            : ChessPiece.Empty;
    }

    /// <summary>Snapshot of playable moves; empty after completion. Does not mutate the game.</summary>
    public IReadOnlyList<CoordinateMove> GetLegalMoves() => IsFinished
        ? Array.Empty<CoordinateMove>()
        : Array.AsReadOnly(LegalMoves(position).Select(ToPublicMove).ToArray());

    public bool HasLegalMoves => !IsFinished && LegalMoves(position).Count != 0;

    public MoveResult MakeMove(CoordinateMove move)
    {
        if (IsFinished || !TryFindMove(move, out int encoded)) return MoveResult.None;
        var next = position;
        MoveDriver.MakeMove(ref next, encoded);
        next.SwitchColor();
        var replies = LegalMoves(next);
        string key = RepetitionKey(next, replies);
        int count = repetitions.GetValueOrDefault(key) + 1;
        var outcome = Evaluate(next, replies.Count, count);
        MoveResult result = MoveResult.Move;
        if (BinaryMoveOps.DecodeCapture(encoded) != 0) result |= MoveResult.Capture;
        if (next.IsKingChecked()) result |= MoveResult.Check;
        if (outcome.Reason == FinishReason.Checkmate) result |= MoveResult.Checkmate;
        if (outcome.Reason == FinishReason.Stalemate) result |= MoveResult.Stalemate;

        var entry = new MoveHistoryEntry(move, ToFen(), FenStrings.FormatUnchecked(next), result);
        undo.Add(position);
        history.Add(entry);
        keys.Add(key);
        repetitions[key] = count;
        position = next;
        Outcome = outcome;
        return result;
    }

    /// <summary>Undo the last successful move, also clearing a later draw claim.</summary>
    public bool UndoMove()
    {
        if (undo.Count == 0) return false;
        string key = keys[^1];
        if (--repetitions[key] == 0) repetitions.Remove(key);
        keys.RemoveAt(keys.Count - 1);
        position = undo[^1];
        undo.RemoveAt(undo.Count - 1);
        history.RemoveAt(history.Count - 1);
        Outcome = Evaluate(position, LegalMoves(position).Count, RepetitionCount);
        return true;
    }

    /// <summary>Claims available after an intended legal move, without executing it.</summary>
    public DrawClaim GetAvailableDrawClaims(CoordinateMove intendedMove)
    {
        if (IsFinished || !TryFindMove(intendedMove, out int encoded)) return DrawClaim.None;
        var next = position;
        MoveDriver.MakeMove(ref next, encoded);
        next.SwitchColor();
        var moves = LegalMoves(next);
        return Claims(next, repetitions.GetValueOrDefault(RepetitionKey(next, moves)) + 1);
    }

    /// <summary>A valid intended-move claim ends the game at its current board; the move is not played.</summary>
    public bool ClaimDraw(DrawClaim reason, CoordinateMove? intendedMove = null)
    {
        if (reason is not (DrawClaim.ThreefoldRepetition or DrawClaim.FiftyMoveRule)) return false;
        DrawClaim available = intendedMove is { } move ? GetAvailableDrawClaims(move) : AvailableDrawClaims;
        if ((available & reason) == 0) return false;
        Outcome = Draw(reason == DrawClaim.ThreefoldRepetition ? FinishReason.ThreefoldRepetition : FinishReason.FiftyMoveRule);
        return true;
    }

    private bool TryFindMove(CoordinateMove move, out int encoded)
    {
        encoded = 0;
        if (!move.IsValid) return false;
        foreach (int candidate in LegalMoves(position))
        {
            if (ToPublicMove(candidate) != move) continue;
            encoded = candidate;
            return true;
        }
        return false;
    }

    private static CoordinateMove ToPublicMove(int move) => new(new Square(BinaryMoveOps.DecodeSrc(move)),
        new Square(BinaryMoveOps.DecodeTrg(move)), BinaryMoveOps.DecodePromotion(move).ToPublicPromotion());

    private static List<int> LegalMoves(Position p)
    {
        Span<int> buffer = stackalloc int[MoveGen.MaxMoves];
        int written = MoveGen.WriteMoves(ref p, p.color, buffer);
        List<int> legal = [];
        for (int i = 0; i < written; i++)
        {
            var next = p;
            // Move legality is independent of FEN counters.
            MoveDriver.MakeMove(ref next, buffer[i], updateCounters: false);
            if (!next.IsKingChecked(p.color)) legal.Add(buffer[i]);
        }
        return legal;
    }

    private static string RepetitionKey(Position p, List<int> moves)
    {
        if (!moves.Any(m => BinaryMoveOps.DecodeEnpassant(m) != 0)) p.enpassant = Squares.Empty;
        string fen = FenStrings.FormatUnchecked(p);
        return fen[..fen.LastIndexOf(' ', fen.LastIndexOf(' ') - 1)];
    }

    private static DrawClaim Claims(Position p, int count) =>
        (count >= 3 ? DrawClaim.ThreefoldRepetition : DrawClaim.None)
        | (p.halfMoveClock >= 100 ? DrawClaim.FiftyMoveRule : DrawClaim.None);

    private static GameOutcome Evaluate(Position p, int legalCount, int repetitions)
    {
        if (legalCount == 0)
        {
            if (!p.IsKingChecked()) return Draw(FinishReason.Stalemate);
            PieceColor winner = Colors.Mirror(p.color).ToPublicColor();
            return new(winner == PieceColor.White ? GameResult.WhiteWin : GameResult.BlackWin, winner, FinishReason.Checkmate);
        }
        if (IsBasicDeadPosition(p)) return Draw(FinishReason.DeadPosition);
        if (repetitions >= 5) return Draw(FinishReason.FivefoldRepetition);
        if (p.halfMoveClock >= 150) return Draw(FinishReason.SeventyFiveMoveRule);
        return GameOutcome.Ongoing;
    }

    private static GameOutcome Draw(FinishReason reason) => new(GameResult.Draw, PieceColor.None, reason);

    private static bool IsBasicDeadPosition(Position p)
    {
        if ((p.pieceBBs[0] | p.pieceBBs[6] | p.pieceBBs[3] | p.pieceBBs[9]
            | p.pieceBBs[4] | p.pieceBBs[10]) != 0) return false;
        ulong knights = p.pieceBBs[1] | p.pieceBBs[7];
        ulong bishops = p.pieceBBs[2] | p.pieceBBs[8];
        if (BitOperations.PopCount(knights | bishops) <= 1) return true;
        // Bishops alone, all on one square color. Never infer deadness from inability to force mate.
        return knights == 0 && ((bishops & 0x55aa55aa55aa55aaUL) == 0 || (bishops & 0xaa55aa55aa55aa55UL) == 0);
    }

    public static bool TryCreateFromFen(string? fen, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out ChessGame? chessGame)
    {
        chessGame = FenStrings.TryParse(fen, out var position) ? new ChessGame(position) : null;
        return chessGame is not null;
    }

    /// <summary>Creates a game from a valid six-field Forsyth-Edwards Notation string.</summary>
    /// <exception cref="FormatException">The FEN does not describe a valid standard-chess position.</exception>
    public static ChessGame FromFen(string fen)
        => TryCreateFromFen(fen, out var game) ? game : throw new FormatException("Invalid standard-chess FEN.");
}
