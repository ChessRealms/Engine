using BenchmarkDotNet.Attributes;
using ChessRealms.Engine.Core.Types;
using ChessRealms.Engine.Parsing;

namespace ChessRealms.Engine.Benchmark;

[MemoryDiagnoser]
public class FenBenchmarks
{
    private Position position;
    private ChessGame game = null!;
    private char[] destination = null!;

    [Params("Start", "Kiwipete", "EnPassant", "LargeCounters")]
    public string Scenario { get; set; } = "Start";

    [GlobalSetup]
    public void Setup()
    {
        string fen = Scenario switch
        {
            "Start" => ChessGame.StartingFen,
            "Kiwipete" => "r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1",
            "EnPassant" => "7k/8/8/3pP3/8/8/8/7K w - d6 0 1",
            "LargeCounters" => $"7k/8/8/8/8/8/8/7K b - - {new string('9', 512)} 1{new string('0', 512)}",
            _ => throw new InvalidOperationException("Unknown FEN benchmark scenario.")
        };
        game = ChessGame.FromFen(fen);
        position = game.Position;
        destination = new char[fen.Length];
        if (FenStrings.FormatUnchecked(position) != fen)
        {
            throw new InvalidOperationException("Invalid FEN benchmark fixture.");
        }

        if (!game.TryFormatFen(destination, out int written) || !destination.AsSpan(0, written).SequenceEqual(fen))
        {
            throw new InvalidOperationException("Invalid span FEN benchmark fixture.");
        }
    }

    [Benchmark]
    public string FormatString() => FenStrings.FormatUnchecked(position);

    [Benchmark]
    public int FormatSpan() => game.TryFormatFen(destination, out int written)
        ? written : throw new InvalidOperationException("The FEN buffer must fit the fixture.");

    [Benchmark]
    public string FormatPositionFields() => FenStrings.FormatPositionFields(position);
}
