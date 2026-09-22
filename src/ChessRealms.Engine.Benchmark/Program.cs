using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.InProcess.Emit;
using ChessRealms.Engine.Benchmark;

Console.WriteLine("Hello, Benckmarks!");
Console.WriteLine();

// Preserve the original full run; allow CLI job selection for smoke checks.
IConfig config = args.Length == 0 ? DefaultConfig.Instance
    .AddJob(Job
         .MediumRun
         .WithLaunchCount(1)
         .WithToolchain(InProcessEmitToolchain.DontLogOutput))
    : DefaultConfig.Instance;

BenchmarkRunner.Run<PerftBenchmarks>(config, args: args);
