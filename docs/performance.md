# Running performance benchmarks

The benchmark project measures execution time and managed allocations with
BenchmarkDotNet. Run from the repository root after restoring dependencies:

```sh
dotnet build src/ChessRealms.Engine.Benchmark -c Release --no-restore
dotnet run --project src/ChessRealms.Engine.Benchmark -c Release --no-build -- --filter '*CoreBenchmarks*' '*GameApiBenchmarks*' '*SanBenchmarks*' '*PgnBenchmarks*' --inProcess --job short --warmupCount 3 --iterationCount 8 --iterationTime 150 --artifacts artifacts/benchmarks/local --exporters JSON
```

Use a single filter to focus on one suite:

| Suite | Coverage |
| --- | --- |
| `CoreBenchmarks` | Pseudolegal generation, depth-three perft and FEN formatting for the start position and Kiwipete. |
| `GameApiBenchmarks` | Legal moves, FEN, history, cloning, move + undo, records and JSON at 0 and 20 plies. |
| `SanBenchmarks` | Span/string formatting, parsing and move + undo for opening, ambiguous and mating moves. |
| `PgnBenchmarks` | Snapshot, export, import and restoration at 0, 20 and 120 plies. |

Setup and input preparation are outside measurements. PGN scenarios use prefixes
of the same validated line. Move + undo returns to the same position, with
collections already warmed; restoration creates a new game including replay
and collection growth. Export to `TextWriter.Null` excludes output storage,
encoding and I/O. Running without arguments starts the existing depth-six perft
benchmark, which can take longer.

For longer measurements in isolated benchmark processes, omit `--inProcess`
and the short iteration settings. Compare the same fixtures on the same machine
and runtime, and inspect confidence intervals and benchmark warnings before
drawing conclusions from timing differences.

## Allocation cross-check

```sh
dotnet run --project src/ChessRealms.Engine.Benchmark -c Release --no-build -- --allocation-probe
```

This emits CSV using `GC.GetAllocatedBytesForCurrentThread` around synchronous
operations after warmup. Setup, delegates and output are excluded. It helps
cross-check small allocation readings from the in-process benchmark harness;
its different warmup duration means results need not match BDN byte for byte.
It does not measure execution time, retained memory or peak live heap.

Keep generated reports and temporary audit notes under `artifacts/benchmarks/`,
which Git ignores. Record the revision, runtime and command with results used
in a PR. Rerun relevant scenarios after changes instead of treating historical
numbers as current API guarantees.

## API usage

`History`, `GetLegalMoves()` and `Clone()` return independently owned snapshots
or games. Read a history snapshot once before traversing it. `ToFen()` reuses an
existing FEN string; `FenBefore` references the preceding position's string.
The [SAN guide](san.md) covers span queries and text application; the
[PGN guide](pgn.md) covers model ownership, export and restoration.
