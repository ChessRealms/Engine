using System.Globalization;

namespace ChessRealms.Engine.Benchmark;

// Complements BenchmarkDotNet's process-wide memory diagnoser. All work is synchronous;
// delegates, fixture setup, output and result consumption are outside the measured region.
internal static class AllocationProbe
{
    internal static void Run()
    {
        Console.WriteLine("Suite,Scenario,Operation,BytesPerOperation");
        foreach (string scenario in new[] { "Start", "Kiwipete" })
        {
            var benchmark = new CoreBenchmarks { Scenario = scenario };
            benchmark.Setup();
            Measure("Core", scenario, "GeneratePseudoLegalMoves", benchmark.GeneratePseudoLegalMoves);
            Measure("Core", scenario, "PerftDepth3", benchmark.PerftDepth3);
            Measure("Core", scenario, "FormatFen", benchmark.FormatFen);
        }

        foreach (int plies in new[] { 0, 20 })
        {
            var benchmark = new GameApiBenchmarks { MoveCount = plies };
            benchmark.Setup();
            string scenario = plies.ToString(CultureInfo.InvariantCulture);
            Measure("GameApi", scenario, "LegalMoves", benchmark.LegalMoves);
            Measure("GameApi", scenario, "Fen", benchmark.Fen);
            Measure("GameApi", scenario, "HistorySnapshot", benchmark.HistorySnapshot);
            Measure("GameApi", scenario, "Clone", benchmark.Clone);
            Measure("GameApi", scenario, "MakeAndUndoMove", benchmark.MakeAndUndoMove);
            Measure("GameApi", scenario, "ExportRecord", benchmark.ExportRecord);
            Measure("GameApi", scenario, "RestoreRecord", benchmark.RestoreRecord);
            Measure("GameApi", scenario, "Serialize", benchmark.Serialize);
            Measure("GameApi", scenario, "Deserialize", benchmark.Deserialize);
        }

        foreach (string scenario in new[] { "Opening", "Ambiguous", "Mate" })
        {
            var benchmark = new SanBenchmarks { Scenario = scenario };
            benchmark.Setup();
            Measure("SAN", scenario, "FormatString", benchmark.FormatString);
            Measure("SAN", scenario, "FormatSpan", benchmark.FormatSpan);
            Measure("SAN", scenario, "ParseSpan", benchmark.ParseSpan);
            Measure("SAN", scenario, "CoordinateMakeAndUndo", benchmark.CoordinateMakeAndUndo);
            Measure("SAN", scenario, "ParseMakeAndUndo", benchmark.ParseMakeAndUndo);
            Measure("SAN", scenario, "SanMakeAndUndo", benchmark.SanMakeAndUndo);
        }

        foreach (int plies in new[] { 0, 20, 120 })
        {
            var benchmark = new PgnBenchmarks { MoveCount = plies };
            benchmark.Setup();
            string scenario = plies.ToString(CultureInfo.InvariantCulture);
            Measure("PGN", scenario, "Snapshot", benchmark.Snapshot);
            Measure("PGN", scenario, "ExportString", benchmark.ExportString);
            Measure("PGN", scenario, "ExportToNullWriter", () =>
            {
                benchmark.ExportToNullWriter();
                return 0;
            });
            Measure("PGN", scenario, "ImportSpan", benchmark.ImportSpan);
            Measure("PGN", scenario, "Restore", benchmark.Restore);
            Measure("PGN", scenario, "ImportAndRestore", benchmark.ImportAndRestore);
        }
    }

    private static void Measure<T>(string suite, string scenario, string operation, Func<T> action)
    {
        T? last = default;
        for (int i = 0; i < 32; ++i)
        {
            last = action();
        }

        const int samples = 128;
        long start = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < samples; ++i)
        {
            last = action();
        }

        long bytes = GC.GetAllocatedBytesForCurrentThread() - start;
        GC.KeepAlive(last);
        Console.WriteLine($"{suite},{scenario},{operation},{(bytes / (double)samples).ToString(CultureInfo.InvariantCulture)}");
    }
}
