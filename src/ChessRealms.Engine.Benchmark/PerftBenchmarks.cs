using BenchmarkDotNet.Attributes;
using ChessRealms.Engine.Core.Attacks;
using ChessRealms.Engine.Core.Types;
using ChessRealms.Engine.Parsing;
using System.Runtime.CompilerServices;

namespace ChessRealms.Engine.Benchmark;

public class PerftBenchmarks
{
    private Position position;

    public PerftBenchmarks()
    {
        _ = FenStrings.TryParse(FenStrings.StartPosition, out position);
        AttackLookups.InvokeInit();
    }

    [Benchmark]
    [MethodImpl(MethodImplOptions.NoOptimization)]
    public void StartPos_Depth_6()
    {
        Perft.PerftDriver.Test(position, 6, false);
    }
}
