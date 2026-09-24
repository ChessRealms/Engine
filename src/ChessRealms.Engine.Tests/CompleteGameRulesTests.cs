using ChessRealms.Engine.Core.Constants;
using ChessRealms.Engine.Core.Types;
using ChessRealms.Engine.Core.Movements;
using ChessRealms.Engine.Parsing;
using System.Numerics;

namespace ChessRealms.Engine.Tests;

internal class CompleteGameRulesTests
{
    private static ChessGame Game(string fen)
    {
        if (!ChessGame.TryCreateFromFen(fen, out var game)) Assert.Fail("Invalid fixture: " + fen);
        return game!;
    }

    private static void Play(ChessGame game, string moves)
    {
        foreach (string move in moves.Split(' '))
            Assert.That(game.MakeMove(CoordinateMove.Parse(move)), Is.Not.EqualTo(MoveResult.None), move);
    }

    private static string State(ChessGame game) => string.Join("|", game.ToFen(), game.Outcome, game.State,
        game.RepetitionCount, game.AvailableDrawClaims, string.Join(";", game.History),
        string.Join(",", game.GetLegalMoves()));

    private static ChessPiece At(ChessGame game, string square)
    {
        var board = new ChessPiece[64];
        game.CopyBoardTo(board);
        return board[Square.Parse(square).Index];
    }

    [TestCase("")]
    [TestCase("e2e4x")]
    [TestCase("e2e4qq")]
    [TestCase("E2e4")]
    [TestCase("e2e4 ")]
    [TestCase(" e2e4")]
    [TestCase("i2e4")]
    [TestCase("e0e4")]
    [TestCase("e2e2")]
    [TestCase("a7a8Q")]
    [TestCase("a7a8p")]
    [TestCase("a7a8k")]
    public void CoordinateInput_IsStrict(string input)
    {
        Assert.That(CoordinateMove.TryParse(input, out var move), Is.False);
        Assert.That(move, Is.EqualTo(default(CoordinateMove)));
        Assert.Throws<FormatException>(() => CoordinateMove.Parse(input));
    }

    [TestCase("e2e4", PieceValue.None)]
    [TestCase("a7a8q", PieceValue.Queen)]
    [TestCase("a7a8r", PieceValue.Rook)]
    [TestCase("a7a8b", PieceValue.Bishop)]
    [TestCase("a7a8n", PieceValue.Knight)]
    public void CoordinateInput_RoundTrips(string input, PieceValue promotion)
    {
        Assert.That(CoordinateMove.TryParse(input, out var move), Is.True);
        Assert.That(move.Promotion, Is.EqualTo(promotion));
        Assert.That(move.ToString(), Is.EqualTo(input));
        Assert.That(move, Is.EqualTo(CoordinateMove.Parse(input)));
    }

    [Test]
    public void InitialPosition_ClocksRightsAndPositionCopy()
    {
        ChessGame game = new();
        Assert.That(game.ToFen(), Is.EqualTo(FenStrings.StartPosition));
        Assert.That(FenStrings.Format(Position.CreateDefault()), Is.EqualTo(FenStrings.StartPosition));
        Assert.That(game.GetLegalMoves(), Has.Count.EqualTo(20));
        var snapshot = game.Position;
        snapshot.PopPieceAt(Squares.a1, Pieces.Rook, Colors.White);
        Assert.That(game.ToFen(), Is.EqualTo(FenStrings.StartPosition));
    }

    [Test]
    public void RejectedMovesAndSnapshots_CannotMutateOwnedHistory()
    {
        ChessGame game = new();
        var oldHistory = game.History;
        var oldMoves = game.GetLegalMoves();
        Play(game, "e2e4");
        Assert.That(oldHistory, Is.Empty);
        Assert.That(oldMoves, Has.Count.EqualTo(20));
        string before = State(game);
        foreach (var move in new[] { default(CoordinateMove), CoordinateMove.Parse("e7e5q"),
            CoordinateMove.Parse("e2e4") })
            Assert.That(game.MakeMove(move), Is.EqualTo(MoveResult.None));
        Assert.Throws<ArgumentException>(() => new CoordinateMove(new Square(Squares.e7),
            new Square(Squares.e5), (PieceValue)99));
        var history = (IList<MoveHistoryEntry>)game.History;
        Assert.Throws<NotSupportedException>(() => history.Clear());
        var legal = (IList<CoordinateMove>)game.GetLegalMoves();
        Assert.Throws<NotSupportedException>(() => legal[0] = default);
        Assert.That(State(game), Is.EqualTo(before));
    }

    [Test]
    public void Promotions_ApplyExactlyOneVariant_AndUndo(
        [Values(false, true)] bool black, [Values(false, true)] bool capture,
        [Values("q", "r", "b", "n")] string suffix)
    {
        string fen = black
            ? capture ? "7k/8/8/8/8/8/p7/1R5K b - - 17 9" : "7k/8/8/8/8/8/p7/7K b - - 17 9"
            : capture ? "1r5k/P7/8/8/8/8/8/7K w - - 17 9" : "7k/P7/8/8/8/8/8/7K w - - 17 9";
        var game = Game(fen);
        string src = black ? "a2" : "a7";
        string trg = (capture ? "b" : "a") + (black ? "1" : "8");
        string before = State(game);
        var moves = game.GetLegalMoves().Where(m => m.Source == Square.Parse(src)
            && m.Target == Square.Parse(trg)).ToArray();
        Assert.That(moves, Has.Length.EqualTo(4));
        Assert.That(moves.Select(m => m.Promotion), Is.EquivalentTo(
            new[] { PieceValue.Queen, PieceValue.Rook, PieceValue.Bishop, PieceValue.Knight }));
        Assert.That(game.MakeMove(CoordinateMove.Parse(src + trg)), Is.EqualTo(MoveResult.None));
        Assert.That(State(game), Is.EqualTo(before));
        var selected = CoordinateMove.Parse(src + trg + suffix);
        var result = game.MakeMove(selected);
        Assert.That(result.HasFlag(MoveResult.Move), Is.True);
        Assert.That(result.HasFlag(MoveResult.Capture), Is.EqualTo(capture));
        Assert.That(At(game, src), Is.EqualTo(ChessPiece.Empty));
        Assert.That(At(game, trg), Is.EqualTo(new ChessPiece(black ? PieceColor.Black : PieceColor.White, selected.Promotion)));
        Assert.That(game.History, Has.Count.EqualTo(1));
        Assert.That(game.HalfmoveClock.IsZero, Is.True);
        Assert.That(game.FullmoveNumber, Is.EqualTo(new BigInteger(black ? 10 : 9)));
        AssertInvariants(game.Position);
        Assert.That(game.UndoMove(), Is.True);
        Assert.That(State(game), Is.EqualTo(before));
    }

    [TestCase("7k/6Q1/6K1/8/8/8/8/8 b - - 150 1", FinishReason.Checkmate, PieceColor.White)]
    [TestCase("8/8/8/8/8/6k1/6q1/7K w - - 150 1", FinishReason.Checkmate, PieceColor.Black)]
    [TestCase("7k/5Q2/6K1/8/8/8/8/8 b - - 0 1", FinishReason.Stalemate, PieceColor.None)]
    [TestCase("8/8/8/8/8/6k1/5q2/7K w - - 0 1", FinishReason.Stalemate, PieceColor.None)]
    public void LoadedTerminalPosition_IsClassifiedAndRejectsMoves(string fen, FinishReason reason, PieceColor winner)
    {
        var game = Game(fen);
        Assert.That(game.IsFinished, Is.True);
        Assert.That(game.Outcome.Reason, Is.EqualTo(reason));
        Assert.That(game.Outcome.Winner, Is.EqualTo(winner));
        Assert.That(game.GetLegalMoves(), Is.Empty);
        string before = State(game);
        Assert.That(game.MakeMove(CoordinateMove.Parse("h8h7")), Is.EqualTo(MoveResult.None));
        Assert.That(game.ClaimDraw(DrawClaim.FiftyMoveRule), Is.False);
        Assert.That(game.UndoMove(), Is.False);
        Assert.That(State(game), Is.EqualTo(before));
    }

    [Test]
    public void Mate_SwitchesSide_RejectsFurtherMoves_AndUndoes()
    {
        ChessGame game = new();
        Play(game, "f2f3 e7e5 g2g4");
        string beforeMate = State(game);
        var result = game.MakeMove(CoordinateMove.Parse("d8h4"));
        Assert.That(result.HasFlag(MoveResult.Checkmate), Is.True);
        Assert.That(game.CurrentColor, Is.EqualTo(PieceColor.White));
        Assert.That(game.Outcome, Is.EqualTo(new GameOutcome(GameResult.BlackWin, PieceColor.Black, FinishReason.Checkmate)));
        string afterMate = State(game);
        Assert.That(game.MakeMove(CoordinateMove.Parse("a7a6")), Is.EqualTo(MoveResult.None));
        Assert.That(game.MakeMove(CoordinateMove.Parse("a2a3")), Is.EqualTo(MoveResult.None));
        Assert.That(State(game), Is.EqualTo(afterMate));
        Assert.That(game.UndoMove(), Is.True);
        Assert.That(State(game), Is.EqualTo(beforeMate));
    }

    [TestCase("w", "e1g1", "g1", "f1", "kq")]
    [TestCase("w", "e1c1", "c1", "d1", "kq")]
    [TestCase("b", "e8g8", "g8", "f8", "KQ")]
    [TestCase("b", "e8c8", "c8", "d8", "KQ")]
    public void Castling_AllFourMoves(string side, string move, string king, string rook, string rights)
    {
        var game = Game("r3k2r/8/8/8/8/8/8/R3K2R " + side + " KQkq - 7 3");
        string before = State(game);
        Play(game, move);
        var color = side == "w" ? PieceColor.White : PieceColor.Black;
        Assert.That(At(game, king), Is.EqualTo(new ChessPiece(color, PieceValue.King)));
        Assert.That(At(game, rook), Is.EqualTo(new ChessPiece(color, PieceValue.Rook)));
        Assert.That(game.ToFen().Split(' ')[2], Is.EqualTo(rights));
        Assert.That(game.HalfmoveClock, Is.EqualTo(new BigInteger(8)));
        Assert.That(game.FullmoveNumber, Is.EqualTo(new BigInteger(side == "b" ? 4 : 3)));
        Assert.That(game.UndoMove(), Is.True);
        Assert.That(State(game), Is.EqualTo(before));
    }

    [TestCase("w", "a1a2", "Kkq")]
    [TestCase("w", "h1h2", "Qkq")]
    [TestCase("w", "e1e2", "kq")]
    [TestCase("b", "a8a7", "KQk")]
    [TestCase("b", "h8h7", "KQq")]
    [TestCase("b", "e8e7", "KQ")]
    [TestCase("w", "a1a8", "Kk")]
    [TestCase("w", "h1h8", "Qq")]
    [TestCase("b", "a8a1", "Kk")]
    [TestCase("b", "h8h1", "Qq")]
    public void MovingKingRookOrCapturingRook_LosesRights(string side, string move, string rights)
    {
        var game = Game("r3k2r/8/8/8/8/8/8/R3K2R " + side + " KQkq - 0 1");
        string before = State(game);
        Play(game, move);
        Assert.That(game.ToFen().Split(' ')[2], Is.EqualTo(rights));
        game.UndoMove();
        Assert.That(State(game), Is.EqualTo(before));
    }

    [TestCase("4kr2/8/8/8/8/8/8/R3K2R w KQ - 0 1", "e1g1")]
    [TestCase("r3k2r/8/8/8/8/8/8/4KR2 b kq - 0 1", "e8g8")]
    [TestCase("4k3/8/8/8/8/8/8/R2bK2R w KQ - 0 1", "e1c1")]
    [TestCase("r2Bk2r/8/8/8/8/8/8/4K3 b kq - 0 1", "e8c8")]
    [TestCase("4r1k1/8/8/8/8/8/8/R3K2R w KQ - 0 1", "e1g1")]
    public void CastlingThroughCheckOrBlockers_IsRejected(string fen, string move)
    {
        var game = Game(fen);
        string before = State(game);
        Assert.That(game.GetLegalMoves(), Does.Not.Contain(CoordinateMove.Parse(move)));
        Assert.That(game.MakeMove(CoordinateMove.Parse(move)), Is.EqualTo(MoveResult.None));
        Assert.That(State(game), Is.EqualTo(before));
    }

    [TestCase("7k/8/8/3pP3/8/8/8/7K w - d6 0 1", "e5d6", "d5")]
    [TestCase("7k/8/8/8/3Pp3/8/8/7K b - d3 0 1", "e4d3", "d4")]
    public void EnPassant_CapturesAndUndoes(string fen, string move, string captured)
    {
        var game = Game(fen);
        string before = State(game);
        Assert.That(game.MakeMove(CoordinateMove.Parse(move)).HasFlag(MoveResult.Capture), Is.True);
        Assert.That(At(game, captured), Is.EqualTo(ChessPiece.Empty));
        Assert.That(game.ToFen().Split(' ')[3], Is.EqualTo("-"));
        Assert.That(game.HalfmoveClock.IsZero, Is.True);
        AssertInvariants(game.Position);
        game.UndoMove();
        Assert.That(State(game), Is.EqualTo(before));
    }

    [TestCase("7k/8/8/r4pPK/8/8/8/8 w - f6 0 1", "g5f6")]
    [TestCase("8/8/8/8/R4Ppk/8/8/7K b - f3 0 1", "g4f3")]
    [TestCase("k3r3/8/8/3pP3/8/8/8/4K3 w - d6 0 1", "e5d6")]
    public void EnPassant_ExposingOwnKingIsIllegal(string fen, string move)
    {
        var game = Game(fen);
        string before = State(game);
        Assert.That(game.GetLegalMoves(), Does.Not.Contain(CoordinateMove.Parse(move)));
        Assert.That(game.MakeMove(CoordinateMove.Parse(move)), Is.EqualTo(MoveResult.None));
        Assert.That(State(game), Is.EqualTo(before));
    }

    [Test]
    public void DoublePush_AlwaysRecordsTarget_AndEnPassantExpires()
    {
        ChessGame game = new();
        Play(game, "e2e4");
        Assert.That(game.ToFen(), Does.EndWith("b KQkq e3 0 1"));
        Play(game, "h7h5");
        Assert.That(game.ToFen(), Does.EndWith("w KQkq h6 0 2"));
        Play(game, "e4e5 d7d5");
        Assert.That(game.GetLegalMoves(), Does.Contain(CoordinateMove.Parse("e5d6")));
        Play(game, "g1f3 g8f6");
        Assert.That(game.GetLegalMoves(), Does.Not.Contain(CoordinateMove.Parse("e5d6")));
    }

    [Test]
    public void Clocks_ClaimsAndAutomaticDraw_AreDistinct()
    {
        var game = Game("4k3/8/8/8/8/8/8/R3K3 w - - 99 50");
        string before = State(game);
        Assert.That(game.GetAvailableDrawClaims(CoordinateMove.Parse("a1a2")), Is.EqualTo(DrawClaim.FiftyMoveRule));
        Assert.That(State(game), Is.EqualTo(before));
        Play(game, "a1a2");
        Assert.That(game.HalfmoveClock, Is.EqualTo(new BigInteger(100)));
        Assert.That(game.FullmoveNumber, Is.EqualTo(new BigInteger(50)));
        Assert.That(game.IsFinished, Is.False);
        Assert.That(game.AvailableDrawClaims, Is.EqualTo(DrawClaim.FiftyMoveRule));
        Play(game, "e8e7");
        Assert.That(game.HalfmoveClock, Is.EqualTo(new BigInteger(101)));
        Assert.That(game.FullmoveNumber, Is.EqualTo(new BigInteger(51)));
        Assert.That(game.ClaimDraw(DrawClaim.FiftyMoveRule), Is.True);
        Assert.That(game.Outcome.Reason, Is.EqualTo(FinishReason.FiftyMoveRule));
        Assert.That(game.UndoMove(), Is.True);
        Assert.That(game.IsFinished, Is.False);
        var automatic = Game("4k3/8/8/8/8/8/8/R3K3 w - - 149 80");
        Play(automatic, "a1a2");
        Assert.That(automatic.Outcome.Reason, Is.EqualTo(FinishReason.SeventyFiveMoveRule));
        Assert.That(automatic.AvailableDrawClaims, Is.EqualTo(DrawClaim.None));
        Assert.That(automatic.UndoMove(), Is.True);
        Assert.That(automatic.HalfmoveClock, Is.EqualTo(new BigInteger(149)));
        Assert.That(automatic.IsFinished, Is.False);
    }

    [TestCase("7k/5Q2/6K1/8/8/8/8/8 w - - 149 80", "f7g7", PieceColor.White)]
    [TestCase("8/8/8/8/8/6k1/5q2/7K b - - 149 80", "f2g2", PieceColor.Black)]
    public void MatingMove_HasPriorityOver75MoveDraw(string fen, string move, PieceColor winner)
    {
        var game = Game(fen);
        Play(game, move);
        Assert.That(game.HalfmoveClock, Is.EqualTo(new BigInteger(150)));
        Assert.That(game.Outcome.Reason, Is.EqualTo(FinishReason.Checkmate));
        Assert.That(game.Outcome.Winner, Is.EqualTo(winner));
    }

    [Test]
    public void IntendedClaim_DoesNotPlayMove_AndInvalidClaimsDoNotMutate()
    {
        var game = Game("4k3/8/8/8/8/8/8/R3K3 w - - 99 50");
        string before = State(game);
        Assert.That(game.ClaimDraw(DrawClaim.FiftyMoveRule), Is.False);
        Assert.That(game.ClaimDraw(DrawClaim.FiftyMoveRule, CoordinateMove.Parse("a1b2")), Is.False);
        Assert.That(game.ClaimDraw((DrawClaim)3, CoordinateMove.Parse("a1a2")), Is.False);
        Assert.That(State(game), Is.EqualTo(before));
        string fen = game.ToFen();
        Assert.That(game.ClaimDraw(DrawClaim.FiftyMoveRule, CoordinateMove.Parse("a1a2")), Is.True);
        Assert.That(game.ToFen(), Is.EqualTo(fen));
        Assert.That(game.History, Is.Empty);
        Assert.That(game.Outcome.Reason, Is.EqualTo(FinishReason.FiftyMoveRule));
    }

    [Test]
    public void Repetition_ClaimsAtThree_AutomaticAtFive_CloneAndUndoPreserveCounts()
    {
        ChessGame game = new();
        const string cycle = "g1f3 g8f6 f3g1 f6g8";
        Play(game, cycle);
        Play(game, "g1f3 g8f6 f3g1");
        Assert.That(game.GetAvailableDrawClaims(CoordinateMove.Parse("f6g8")), Is.EqualTo(DrawClaim.ThreefoldRepetition));
        var claiming = game.Clone();
        string claimFen = claiming.ToFen();
        Assert.That(claiming.ClaimDraw(DrawClaim.ThreefoldRepetition, CoordinateMove.Parse("f6g8")), Is.True);
        Assert.That(claiming.ToFen(), Is.EqualTo(claimFen));
        Assert.That(game.IsFinished, Is.False);
        Play(game, "f6g8");
        Assert.That(game.RepetitionCount, Is.EqualTo(3));
        Assert.That(game.IsFinished, Is.False);
        Assert.That(game.AvailableDrawClaims, Is.EqualTo(DrawClaim.ThreefoldRepetition));
        var claimed = game.Clone();
        Assert.That(claimed.ClaimDraw(DrawClaim.ThreefoldRepetition), Is.True);
        Assert.That(claimed.Outcome.Reason, Is.EqualTo(FinishReason.ThreefoldRepetition));
        Assert.That(game.IsFinished, Is.False);
        Play(game, cycle);
        Assert.That(game.RepetitionCount, Is.EqualTo(4));
        Play(game, "g1f3 g8f6 f3g1");
        string before = State(game);
        var copy = game.Clone();
        Play(game, "f6g8");
        Assert.That(game.RepetitionCount, Is.EqualTo(5));
        Assert.That(game.Outcome.Reason, Is.EqualTo(FinishReason.FivefoldRepetition));
        Assert.That(State(copy), Is.EqualTo(before));
        Assert.That(game.UndoMove(), Is.True);
        Assert.That(State(game), Is.EqualTo(before));
        Assert.That(State(game), Is.EqualTo(State(copy)));
        Play(game, "f6g8");
        Assert.That(game.Outcome.Reason, Is.EqualTo(FinishReason.FivefoldRepetition));
        var loaded = Game(game.ToFen());
        Assert.That(loaded.RepetitionCount, Is.EqualTo(1));
        Assert.That(loaded.History, Is.Empty);
        Assert.That(loaded.IsFinished, Is.False);
    }

    [TestCase("7k/8/8/3p4/8/8/8/7K w - d6 0 1", "h1g1 h8g8 g1h1 g8h8", 2)]
    [TestCase("7k/8/8/3pP3/8/8/8/7K w - d6 0 1", "h1g1 h8g8 g1h1 g8h8", 1)]
    [TestCase("k3r3/8/8/3pP3/8/8/8/4K3 w - d6 0 1", "e1d1 a8b8 d1e1 b8a8", 2)]
    [TestCase("7k/8/8/8/3Pp3/8/8/7K b - d3 0 1", "h8g8 h1g1 g8h8 g1h1", 1)]
    [TestCase("4k3/8/8/8/3Pp3/8/8/K3R3 b - d3 0 1", "e8d8 a1b1 d8e8 b1a1", 2)]
    public void Repetition_UsesOnlyLegallyAvailableEnPassant(string fen, string cycle, int count)
    {
        var game = Game(fen);
        Play(game, cycle);
        Assert.That(game.RepetitionCount, Is.EqualTo(count));
    }

    [Test]
    public void Repetition_DistinguishesLostCastlingRights()
    {
        var game = Game("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1");
        const string cycle = "h1h2 h8h7 h2h1 h7h8";
        Play(game, cycle);
        Assert.That(game.RepetitionCount, Is.EqualTo(1));
        Play(game, cycle);
        Assert.That(game.RepetitionCount, Is.EqualTo(2));
    }

    [TestCase("7k/8/8/8/8/8/8/7K w - - 0 1", true)]
    [TestCase("7k/8/8/8/8/8/8/2B4K w - - 0 1", true)]
    [TestCase("7k/8/8/8/8/8/8/2N4K w - - 0 1", true)]
    [TestCase("2b4k/8/8/8/8/8/8/7K b - - 0 1", true)]
    [TestCase("2n4k/8/8/8/8/8/8/7K b - - 0 1", true)]
    [TestCase("5b1k/8/8/8/8/8/8/2B4K w - - 0 1", true)]
    [TestCase("5b1k/8/8/8/8/4B3/8/2B4K w - - 0 1", true)]
    [TestCase("7k/8/8/8/8/8/8/1NN4K w - - 0 1", false)]
    [TestCase("2n4k/8/8/8/8/8/8/1N5K w - - 0 1", false)]
    [TestCase("2b4k/8/8/8/8/8/8/2B4K w - - 0 1", false)]
    [TestCase("7k/8/8/8/8/8/8/1NB4K w - - 0 1", false)]
    public void DeadPositions_AreConservative(string fen, bool dead)
    {
        var game = Game(fen);
        Assert.That(game.Outcome.Reason, Is.EqualTo(dead ? FinishReason.DeadPosition : FinishReason.None));
    }

    [Test]
    public void CaptureIntoDeadPosition_AndUndo()
    {
        var game = Game("7k/8/8/8/8/4n3/3B4/7K w - - 12 7");
        string before = State(game);
        Play(game, "d2e3");
        Assert.That(game.Outcome.Reason, Is.EqualTo(FinishReason.DeadPosition));
        game.UndoMove();
        Assert.That(State(game), Is.EqualTo(before));
    }

    [TestCase(17)]
    [TestCase(12345)]
    [TestCase(987654)]
    public void DeterministicPlayouts_AllLegalMovesApplyToClones_AndUndoExactly(int seed)
    {
        var random = new Random(seed);
        ChessGame game = new();
        var states = new List<string> { State(game) };
        for (int ply = 0; ply < 160 && !game.IsFinished; ply++)
        {
            string before = State(game);
            var moves = game.GetLegalMoves();
            Assert.That(moves.Distinct().Count(), Is.EqualTo(moves.Count));
            foreach (var move in moves)
            {
                var copy = game.Clone();
                var positionBefore = copy.Position;
                Assert.That(copy.MakeMove(move), Is.Not.EqualTo(MoveResult.None), move.ToString());
                AssertInvariants(copy.Position);
                int previousColor = game.CurrentColor == PieceColor.White ? Colors.White : Colors.Black;
                Assert.That(copy.Position.IsKingChecked(previousColor), Is.False);
                Assert.That(FenStrings.TryParse(copy.ToFen(), out var imported), Is.True);
                Assert.That(FenStrings.Format(imported), Is.EqualTo(copy.ToFen()));
                Assert.That(copy.UndoMove(), Is.True);
                Assert.That(copy.Position, Is.EqualTo(positionBefore));
                Assert.That(State(copy), Is.EqualTo(before));
            }
            Assert.That(State(game), Is.EqualTo(before));
            Assert.That(game.MakeMove(moves[random.Next(moves.Count)]), Is.Not.EqualTo(MoveResult.None));
            AssertInvariants(game.Position);
            states.Add(State(game));
        }
        for (int i = states.Count - 2; i >= 0; i--)
        {
            Assert.That(game.UndoMove(), Is.True);
            Assert.That(State(game), Is.EqualTo(states[i]));
        }
        Assert.That(game.UndoMove(), Is.False);
    }

    [TestCase("2147483647")]
    [TestCase("9223372036854775807")]
    [TestCase("999999999999999999999999999999999999")]
    public void CountersBeyondMachineIntegerRange_AllMovesApplyAndRoundTrip(string fullmove)
    {
        var game = Game("4k3/8/8/8/8/8/8/R3K3 b - - 17 " + fullmove);
        var before = game.Position;
        var expected = BigInteger.Parse(fullmove) + 1;
        foreach (var move in game.GetLegalMoves())
        {
            var copy = game.Clone();
            Assert.That(copy.MakeMove(move), Is.Not.EqualTo(MoveResult.None));
            Assert.That(copy.FullmoveNumber, Is.EqualTo(expected));
            Assert.That(Game(copy.ToFen()).Position, Is.EqualTo(copy.Position));
            Assert.That(copy.UndoMove(), Is.True);
            Assert.That(copy.Position, Is.EqualTo(before));
        }
        Assert.That(game.Position, Is.EqualTo(before));
    }

    private static void AssertInvariants(Position p)
    {
        ulong union = 0;
        for (int color = 0; color < 2; color++)
        {
            ulong occupied = 0;
            for (int piece = 0; piece < 6; piece++)
            {
                ulong bb = p.pieceBBs[color * 6 + piece];
                Assert.That(bb & union, Is.Zero, "Piece bitboards must be disjoint");
                union |= bb;
                occupied |= bb;
            }
            Assert.That(p.blockers[color], Is.EqualTo(occupied));
            Assert.That(BitOperations.PopCount(p.pieceBBs[color * 6 + Pieces.King]), Is.EqualTo(1));
            Assert.That(p.pieceBBs[color * 6] & 0xff000000000000ffUL, Is.Zero);
        }
        Assert.That(p.blockers[2], Is.EqualTo(union));
        Assert.That(p.IsKingChecked(Colors.Mirror(p.color)), Is.False);
    }
}
