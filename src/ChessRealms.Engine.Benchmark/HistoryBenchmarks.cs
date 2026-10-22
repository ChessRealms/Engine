using BenchmarkDotNet.Attributes;

namespace ChessRealms.Engine.Benchmark;

[MemoryDiagnoser]
public class HistoryBenchmarks
{
    private CoordinateMove[] line = null!;
    private ChessGame game = null!;
    private CoordinateMove nextMove;
    private readonly char[] fenBuffer = new char[128];

    [Params(0, 20, 120)]
    public int MoveCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        line = PgnBenchmarks.CreateLongLine().Take(MoveCount).ToArray();
        game = ReplayWithoutFen();
        if (game.History.Count != MoveCount || game.IsFinished)
        {
            throw new InvalidOperationException("The history fixture must have every requested ply and allow a next move.");
        }

        nextMove = game.GetLegalMoves()[0];
    }

    [Benchmark]
    public ChessGame ReplayWithoutFen() => Replay(false, false).Game;

    [Benchmark]
    public (ChessGame Game, int ObservedLength) ReplayWithFen() => Replay(true, false);

    [Benchmark]
    public IReadOnlyList<MoveHistoryEntry> ReplayThenHistory() => ReplayWithoutFen().History;

    [Benchmark]
    public (ChessGame Game, int ObservedLength) ReplayWithHistoryEachMove() => Replay(false, true);

    [Benchmark]
    public IReadOnlyList<MoveHistoryEntry> HistorySnapshot() => game.History;

    [Benchmark]
    public ChessGame Clone() => game.Clone();

    [Benchmark]
    public string HistoricalFenString() => game.GetFenAtPly(MoveCount / 2);

    [Benchmark]
    public int HistoricalFenSpan() => game.TryFormatFenAtPly(MoveCount / 2, fenBuffer, out int written)
        ? written : throw new InvalidOperationException("The FEN buffer must fit the fixture.");

    [Benchmark]
    public MoveResult MakeAndUndo()
    {
        MoveResult result = game.MakeMove(nextMove);
        if (result == MoveResult.None || !game.UndoMove())
        {
            throw new InvalidOperationException("The benchmark must return to the original position.");
        }

        return result;
    }

    [Benchmark]
    public string MakeReadFenAndUndo()
    {
        MoveResult result = game.MakeMove(nextMove);
        string fen = game.ToFen();
        if (result == MoveResult.None || !game.UndoMove())
        {
            throw new InvalidOperationException("The benchmark must return to the original position.");
        }

        return fen;
    }

    private (ChessGame Game, int ObservedLength) Replay(bool readFen, bool readHistory)
    {
        var replayed = new ChessGame();
        int observedLength = 0;
        foreach (var move in line)
        {
            if (replayed.MakeMove(move) == MoveResult.None)
            {
                throw new InvalidOperationException("Invalid history benchmark move.");
            }

            if (readFen)
            {
                observedLength += replayed.ToFen().Length;
            }

            if (readHistory)
            {
                observedLength += replayed.History.Count;
            }
        }

        return (replayed, observedLength);
    }
}
