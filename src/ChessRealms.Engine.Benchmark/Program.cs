using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.InProcess.Emit;
using ChessRealms.Engine.Benchmark;

if (args is ["--allocation-probe"])
{
    AllocationProbe.Run();
    return;
}

if (args is ["--history-memory-probe"])
{
    HistoryMemoryProbe.Run();
    return;
}

Console.WriteLine("ChessRealms benchmarks");
Console.WriteLine();

// Preserve the original full run; allow CLI job selection for smoke checks.
IConfig config = args.Length == 0 ? DefaultConfig.Instance
    .AddJob(Job
         .MediumRun
         .WithLaunchCount(1)
         .WithToolchain(InProcessEmitToolchain.DontLogOutput))
    : DefaultConfig.Instance;

if (args.Length == 0)
{
    BenchmarkRunner.Run<PerftBenchmarks>(config);
}
else
{
    BenchmarkSwitcher.FromAssembly(typeof(PerftBenchmarks).Assembly).Run(args, config);
}
