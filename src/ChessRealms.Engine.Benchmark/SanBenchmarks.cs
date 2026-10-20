using BenchmarkDotNet.Attributes;

namespace ChessRealms.Engine.Benchmark;

[MemoryDiagnoser]
public class SanBenchmarks
{
    private ChessGame game = null!;
    private CoordinateMove move;
    private string san = null!;

    [Params("Opening", "Ambiguous", "Mate")]
    public string Scenario { get; set; } = "Opening";

    [GlobalSetup]
    public void Setup()
    {
        (string fen, string coordinate, string notation) = Scenario switch
        {
            "Opening" => (ChessGame.StartingFen, "e2e4", "e4"),
            "Ambiguous" => ("7k/8/8/8/8/1N6/8/1N3N1K w - - 0 1", "b1d2", "Nb1d2"),
            "Mate" => ("7k/5Q2/6K1/8/8/8/8/8 w - - 0 1", "f7g7", "Qg7#"),
            _ => throw new InvalidOperationException("Unknown SAN benchmark scenario.")
        };
        game = ChessGame.FromFen(fen);
        move = CoordinateMove.Parse(coordinate);
        san = notation;
        if (game.FormatSan(move) != san || game.ParseSan(san) != move)
        {
            throw new InvalidOperationException("Invalid SAN benchmark fixture.");
        }
    }

    [Benchmark]
    public string FormatString() => game.FormatSan(move);

    [Benchmark]
    public int FormatSpan()
    {
        Span<char> destination = stackalloc char[7];
        return game.TryFormatSan(move, destination, out int written) ? written : 0;
    }

    [Benchmark]
    public CoordinateMove ParseSpan() => game.ParseSan(san.AsSpan());

    [Benchmark]
    public MoveResult CoordinateMakeAndUndo() => Undo(game.MakeMove(move));

    [Benchmark]
    public MoveResult ParseMakeAndUndo() => Undo(game.MakeMove(game.ParseSan(san)));

    [Benchmark]
    public MoveResult SanMakeAndUndo() => Undo(game.MakeSanMove(san));

    private MoveResult Undo(MoveResult result)
    {
        if (result == MoveResult.None || !game.UndoMove())
        {
            throw new InvalidOperationException("The benchmark must return to its starting position.");
        }

        return result;
    }
}
