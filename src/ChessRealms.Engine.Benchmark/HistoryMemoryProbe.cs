using System.Globalization;
using System.Runtime.CompilerServices;

namespace ChessRealms.Engine.Benchmark;

// An approximate retained-heap comparison, separate from per-operation allocation measurements.
// Keep the same fixture, game count and runtime when comparing revisions.
internal static class HistoryMemoryProbe
{
    internal static void Run()
    {
        var line = PgnBenchmarks.CreateLongLine();
        WarmUp(line);
        Console.WriteLine("Plies,Observation,RetainedBytesPerGame");
        foreach (int plies in new[] { 0, 20, 120 })
        {
            foreach (string observation in new[] { "None", "CurrentFen", "History" })
            {
                Measure(line, plies, observation);
            }
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void WarmUp(CoordinateMove[] line)
    {
        var game = CreateGame(line, line.Length, "History");
        GC.KeepAlive(game);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Measure(CoordinateMove[] line, int plies, string observation)
    {
        const int samples = 256;
        var games = new ChessGame[samples];
        long before = GC.GetTotalMemory(forceFullCollection: true);
        for (int i = 0; i < samples; i++)
        {
            games[i] = CreateGame(line, plies, observation);
        }

        long bytes = GC.GetTotalMemory(forceFullCollection: true) - before;
        GC.KeepAlive(games);
        Console.WriteLine($"{plies},{observation},{(bytes / (double)samples).ToString(CultureInfo.InvariantCulture)}");
    }

    private static ChessGame CreateGame(CoordinateMove[] line, int plies, string observation)
    {
        var game = new ChessGame();
        for (int i = 0; i < plies; i++)
        {
            if (game.MakeMove(line[i]) == MoveResult.None)
            {
                throw new InvalidOperationException("Invalid retained-memory fixture.");
            }
        }

        if (observation == "CurrentFen")
        {
            _ = game.ToFen();
        }
        else if (observation == "History")
        {
            // The returned snapshot is not retained; measure the game after history was read.
            _ = game.History;
        }

        return game;
    }
}
