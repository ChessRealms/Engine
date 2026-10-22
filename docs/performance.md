# Running performance benchmarks

The benchmark project measures execution time and managed allocations with
BenchmarkDotNet. Run from the repository root after restoring dependencies:

```sh
dotnet build src/ChessRealms.Engine.Benchmark -c Release --no-restore
dotnet run --project src/ChessRealms.Engine.Benchmark -c Release --no-build -- --filter '*CoreBenchmarks*' '*FenBenchmarks*' '*GameApiBenchmarks*' '*HistoryBenchmarks*' '*RecordJsonBenchmarks*' '*SanBenchmarks*' '*PgnBenchmarks*' --inProcess --job short --warmupCount 3 --iterationCount 8 --iterationTime 150 --artifacts artifacts/benchmarks/local --exporters JSON
```

Use a single filter to focus on one suite:

| Suite | Coverage |
| --- | --- |
| `CoreBenchmarks` | Pseudolegal generation, depth-three perft and FEN formatting for the start position and Kiwipete. |
| `FenBenchmarks` | Fresh string and span formatting, and the first four FEN fields, for the start position, Kiwipete, en passant and large counters. |
| `GameApiBenchmarks` | Legal moves, FEN, history, cloning, move + undo, records and JSON at 0 and 20 plies. |
| `HistoryBenchmarks` | Replay with no FEN reads, per-move FEN/history reads, a final history read, warmed snapshots, cloning, historical FEN and move + undo at 0, 20 and 120 plies. |
| `RecordJsonBenchmarks` | Deserialize + restore versus direct JSON restoration, both alone and followed by a move and JSON export, at 0, 20 and 120 plies. |
| `SanBenchmarks` | Span/string formatting, parsing and move + undo for opening, ambiguous and mating moves. |
| `PgnBenchmarks` | Snapshot, export, import and restoration at 0, 20 and 120 plies, including Parse + ToGame versus import with the validated game. |

Setup and input preparation are outside measurements. PGN scenarios use prefixes
of the same validated line. Move + undo returns to the same position, with
collections already warmed; restoration creates a new game including replay
and collection growth. Export to `TextWriter.Null` excludes output storage,
encoding and I/O. Running without arguments starts the existing depth-six perft
benchmark, which can take longer.

`ImportAndRestore` and `ImportWithGame` both parse the same input and create the
PGN document and game. The former replays again through `ToGame()`; the latter
returns the game built during validation. Both benchmark methods return the
game and discard the document after parsing. Setup checks matching document
output and game records outside measurements.

History scenarios use prefixes of the same validated PGN line. Replay creates
a fresh game per operation. Snapshot and individual historical FEN queries use
a game whose strings have already been requested during setup. The move + undo
cases distinguish applying a move alone from reading its FEN before undoing it.

JSON restoration scenarios use the same line and compare equivalent results.
Each operation creates a fresh game; input JSON and the next legal move are
prepared during setup. Move-and-save cases include record export and JSON
serialization, but exclude database, network and other server work.

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

For a separate approximate retained-heap comparison:

```sh
dotnet run --project src/ChessRealms.Engine.Benchmark -c Release --no-build -- --history-memory-probe
```

This retains batches of 256 games at 0, 20 and 120 plies, measuring the heap
after full collections. It distinguishes games with no FEN queries, a current
FEN query, and a complete history query. Returned history snapshots are not kept.
Use the same runtime and fixture for comparisons; GC heap deltas include noise
and are not exact object-size or peak-memory measurements.

Keep generated reports and temporary audit notes under `artifacts/benchmarks/`,
which Git ignores. Record the revision, runtime and command with results used
in a PR. Rerun relevant scenarios after changes instead of treating historical
numbers as current API guarantees.

## API usage

`History`, `GetLegalMoves()` and `Clone()` return independently owned snapshots
or games. Read a history snapshot once before traversing it. Use `PlyCount` for
the recorded half-move count without materializing history. `ToFen()` and
`GetFenAtPly()` format and cache a position's FEN on first request. `History`
materializes all entries, reusing those caches; `FenBefore` references the
preceding position's string.
`TryFormatFen` writes to a caller-owned character span; `FenBenchmarks` measures
formatting separately from reading the cached string through `ToFen()`.
`TryFormatFenAtPly` writes a single historical position without populating the
string cache or materializing history.
Use `ChessGame.FromRecordJson` to restore a playable game directly from record
JSON with one validated replay. `GameRecord.Deserialize` followed by
`ChessGame.FromRecord` performs two replays; keep that path when the separate
record is needed for its own purpose.
The [SAN guide](san.md) covers span queries and text application; the
[PGN guide](pgn.md) covers model ownership, export and restoration.
