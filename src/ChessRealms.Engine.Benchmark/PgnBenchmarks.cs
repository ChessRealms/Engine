using BenchmarkDotNet.Attributes;

namespace ChessRealms.Engine.Benchmark;

[MemoryDiagnoser]
public class PgnBenchmarks
{
    private ChessGame game = null!;
    private PgnGame document = null!;
    private string text = null!;

    [Params(0, 20, 120)]
    public int MoveCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        game = new ChessGame();
        foreach (var move in CreateLongLine().Take(MoveCount))
        {
            if (game.MakeMove(move) == MoveResult.None)
            {
                throw new InvalidOperationException("The benchmark fixture must contain every requested ply.");
            }
        }

        document = PgnGame.FromGame(game);
        text = document.ToPgn();
        var imported = PgnGame.Parse(text);
        if (imported.Moves.Count != MoveCount || imported.ToGame().ToFen() != game.ToFen())
        {
            throw new InvalidOperationException("The benchmark fixture must round-trip.");
        }

        var directDocument = PgnGame.Parse(text, out var directGame);
        if (directDocument.ToPgn() != imported.ToPgn()
            || GameRecord.Serialize(directGame.ExportRecord()) != GameRecord.Serialize(game.ExportRecord()))
        {
            throw new InvalidOperationException("Both import paths must preserve the document and game.");
        }
    }

    internal static CoordinateMove[] CreateLongLine()
    {
        // Use the same 120-ply fixture prefix for every size. Reject short games
        // during setup instead of accidentally measuring fewer plies than advertised.
        for (int seed = 0; seed < 128; ++seed)
        {
            var candidate = new ChessGame();
            var random = new Random(seed);
            int plies = 0;
            while (plies < 120 && !candidate.IsFinished)
            {
                var moves = candidate.GetLegalMoves();
                candidate.MakeMove(moves[random.Next(moves.Count)]);
                ++plies;
            }

            if (plies == 120)
            {
                return candidate.CopyRecordedMoves();
            }
        }

        throw new InvalidOperationException("Could not construct a 120-ply benchmark fixture.");
    }

    [Benchmark]
    public PgnGame Snapshot() => PgnGame.FromGame(game);

    [Benchmark]
    public string ExportString() => document.ToPgn();

    // Measures traversal/formatting without output buffering, encoding or I/O.
    [Benchmark]
    public void ExportToNullWriter() => document.WriteTo(TextWriter.Null);

    [Benchmark]
    public PgnGame ImportSpan() => PgnGame.Parse(text.AsSpan());

    [Benchmark]
    public ChessGame Restore() => document.ToGame();

    [Benchmark]
    public ChessGame ImportAndRestore() => PgnGame.Parse(text.AsSpan()).ToGame();

    [Benchmark]
    public ChessGame ImportWithGame()
    {
        _ = PgnGame.Parse(text.AsSpan(), out var replay);
        return replay;
    }
}
