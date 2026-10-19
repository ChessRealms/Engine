using BenchmarkDotNet.Attributes;

namespace ChessRealms.Engine.Benchmark;

[MemoryDiagnoser]
public class GameApiBenchmarks
{
    private ChessGame game = null!;
    private GameRecord record = null!;
    private string json = null!;
    private CoordinateMove nextMove;

    [Params(0, 20)]
    public int MoveCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        const string line = "e2e4 e7e5 g1f3 b8c6 f1b5 a7a6 b5a4 g8f6 e1g1 f8e7 "
            + "f1e1 b7b5 a4b3 d7d6 c2c3 e8g8 h2h3 c6b8 d2d4 b8d7";
        game = new ChessGame();
        foreach (string text in line.Split(' ').Take(MoveCount))
        {
            if (game.MakeMove(CoordinateMove.Parse(text)) == MoveResult.None)
            {
                throw new InvalidOperationException($"Invalid benchmark move: {text}.");
            }
        }

        nextMove = CoordinateMove.Parse(MoveCount == 0 ? "e2e4" : "b1d2");
        record = game.ExportRecord();
        json = GameRecord.Serialize(record);
    }

    [Benchmark]
    public IReadOnlyList<CoordinateMove> LegalMoves() => game.GetLegalMoves();

    [Benchmark]
    public string Fen() => game.ToFen();

    [Benchmark]
    public MoveResult MakeAndUndoMove()
    {
        var result = game.MakeMove(nextMove);
        if (result == MoveResult.None || !game.UndoMove())
        {
            throw new InvalidOperationException("The benchmark must return to its starting position.");
        }

        return result;
    }

    [Benchmark]
    public GameRecord ExportRecord() => game.ExportRecord();

    [Benchmark]
    public ChessGame RestoreRecord() => ChessGame.FromRecord(record);

    [Benchmark]
    public string Serialize() => GameRecord.Serialize(record);

    [Benchmark]
    public GameRecord Deserialize() => GameRecord.Deserialize(json);
}
