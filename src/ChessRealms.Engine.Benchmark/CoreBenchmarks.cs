using BenchmarkDotNet.Attributes;
using ChessRealms.Engine.Core.Movements;
using ChessRealms.Engine.Core.Types;
using ChessRealms.Engine.Parsing;

namespace ChessRealms.Engine.Benchmark;

[MemoryDiagnoser]
public class CoreBenchmarks
{
    private Position position;

    [Params("Start", "Kiwipete")]
    public string Scenario { get; set; } = "Start";

    [GlobalSetup]
    public void Setup()
    {
        string fen = Scenario == "Start" ? ChessGame.StartingFen
            : "r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1";
        position = ChessGame.FromFen(fen).Position;
        ulong expected = Scenario == "Start" ? 8902UL : 97862UL;
        if (Perft.PerftDriver.Test(position, 3, false).Nodes != expected)
        {
            throw new InvalidOperationException("Invalid perft benchmark fixture.");
        }
    }

    [Benchmark]
    public int GeneratePseudoLegalMoves()
    {
        var copy = position;
        Span<int> moves = stackalloc int[MoveGen.MaxMoves];
        return MoveGen.WriteMoves(ref copy, copy.color, moves);
    }

    [Benchmark]
    public ulong PerftDepth3() => Perft.PerftDriver.Test(position, 3, false).Nodes;

    [Benchmark]
    public string FormatFen() => FenStrings.FormatUnchecked(position);
}
