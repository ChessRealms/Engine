using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;

namespace ChessRealms.Engine.Benchmark;

[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class RecordJsonBenchmarks
{
    private string json = null!;
    private CoordinateMove nextMove;

    [Params(0, 20, 120)]
    public int MoveCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var game = new ChessGame();
        foreach (var move in PgnBenchmarks.CreateLongLine().Take(MoveCount))
        {
            if (game.MakeMove(move) == MoveResult.None)
            {
                throw new InvalidOperationException("The JSON fixture must contain every requested ply.");
            }
        }

        if (game.PlyCount != MoveCount || game.IsFinished)
        {
            throw new InvalidOperationException("The JSON fixture must allow a next move.");
        }

        nextMove = game.GetLegalMoves()[0];
        json = GameRecord.Serialize(game.ExportRecord());
        if (GameRecord.Serialize(DeserializeThenRestore().ExportRecord()) != json
            || GameRecord.Serialize(RestoreDirectly().ExportRecord()) != json
            || DeserializeThenMoveAndSave() != RestoreThenMoveAndSave())
        {
            throw new InvalidOperationException("Both JSON paths must produce the same game record.");
        }
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Restore")]
    public ChessGame DeserializeThenRestore() => ChessGame.FromRecord(GameRecord.Deserialize(json));

    [Benchmark]
    [BenchmarkCategory("Restore")]
    public ChessGame RestoreDirectly() => ChessGame.FromRecordJson(json);

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("MoveAndSave")]
    public string DeserializeThenMoveAndSave() => MoveAndSave(DeserializeThenRestore());

    [Benchmark]
    [BenchmarkCategory("MoveAndSave")]
    public string RestoreThenMoveAndSave() => MoveAndSave(RestoreDirectly());

    private string MoveAndSave(ChessGame game)
    {
        if (game.MakeMove(nextMove) == MoveResult.None)
        {
            throw new InvalidOperationException("The restored game must accept the next move.");
        }

        return GameRecord.Serialize(game.ExportRecord());
    }
}
