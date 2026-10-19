# Measuring game API allocations

`GameApiBenchmarks` measures public operations with an empty game and a game
after 20 plies. Setup, notation parsing and input JSON preparation happen
outside the measured operations. `MakeAndUndoMove` plays one legal move and
undoes it on the same game; warmup has already grown the internal collections.
Restoration and deserialization create a new game each time, including all
validation and replay. Serialization measures an already exported record.

Run from the repository root:

```sh
dotnet build src/ChessRealms.Engine.Benchmark -c Release
dotnet run --project src/ChessRealms.Engine.Benchmark -c Release --no-build -- --filter '*GameApiBenchmarks*' --inProcess --job short --warmupCount 3 --iterationCount 5 --iterationTime 100 --artifacts artifacts/benchmarks/game-api --exporters JSON
```

The benchmark project also supports the usual BenchmarkDotNet arguments. For a
longer run with isolated benchmark processes, omit `--inProcess` and the short
iteration settings. Running without arguments preserves the existing perft
benchmark. Benchmark artifacts are ignored by Git.

## Allocation changes measured on 2026-10-19

The baseline engine was commit `1ea5cc4`, with the same benchmark harness added
before changing the implementation. Both runs used Windows 11 x64, SDK
10.0.402, .NET 10.0.13 and concurrent workstation GC on the same machine.
The command above was used with separate `game-api-before` and `game-api-after`
artifact directories.

Allocated managed bytes per operation:

| Operation | Plies | Before | After |
| --- | ---: | ---: | ---: |
| Get legal moves | 0 | 648 | 208 |
| Read FEN | 0 | 480 | 0 |
| Make and undo a move | 0 | 2776 | 1048 |
| Export record | 0 | 96 | 40 |
| Restore record | 0 | 2728 | 2224 |
| Serialize record | 0 | 664 | 664 |
| Deserialize record | 0 | 3520 | 3016 |
| Get legal moves | 20 | 1056 | 336 |
| Read FEN | 20 | 704 | 0 |
| Make and undo a move | 20 | 3952 | 1416 |
| Export record | 20 | 1176 | 248 |
| Restore record | 20 | 75918 | 40996 |
| Serialize record | 20 | 1224 | 1224 |
| Deserialize record | 20 | 77494 | 42572 |

At 20 plies the mean restoration time changed from 145.4 to 100.1 microseconds,
and deserialization from 149.3 to 104.8 microseconds. These are short local
measurements, not production latency guarantees. BenchmarkDotNet could not set
the power plan and reported some iterations below its recommended 100 ms
minimum. Treat timing differences as indicative; FEN access is now close to
measurement overhead, so no timing ratio is claimed for it.

## Implementation and ownership

- Internal legal-move generation filters moves in a bounded, local span. The
  public `GetLegalMoves()` still returns an independently owned, read-only
  snapshot, including every promotion choice.
- Record export copies coordinate moves directly from the owned history once.
  It avoids the public history snapshot and a second move-array copy. The
  constructor accepting caller-owned moves still copies them defensively.
- `ToFen()` reuses the initial FEN or the last history entry's `FenAfter`.
  Moves already store that string; undo and branching select the correct
  history entry. No additional mutable cache is introduced.
- Repetition keys format only the four position fields, excluding counters.
  En passant is still included only when a legal en passant move exists.
- `HasLegalMoves` uses the already evaluated outcome: a position without legal
  moves has already finished. Claimed and automatic draws also prohibit play.

Public history snapshots, game-record immutability, JSON schema version one and
validated restoration keep their existing contracts. JSON serialization itself
is unchanged in this pass. Its allocation count includes the result string and
serializer overhead. Deserialization still validates by replaying; calling
`ChessGame.FromRecord` afterwards performs a second replay.
