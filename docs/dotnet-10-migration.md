# .NET 10 migration

> Repository organization note: the solution now lives at the repository root
> as `ChessRealms.Engine.slnx`.
> Commands and results below preserve the paths used at the time of validation.
> See [CONTRIBUTING.md](../CONTRIBUTING.md) for current commands.

Validated on 2026-09-10, Windows 11 x64. The starting commit was `d8eb1e3`
(`test: add regression test foundation and .NET 8 CI`), confirmed as `origin/main`
after fetching. The working tree was clean; no applicable `AGENTS.md` was found.
Branch: `feature/dotnet-10`. No chess rules, public API, position representation,
algorithms, test sources or perft reference values were changed.

## SDK and restore

`global.json` pins stable SDK **10.0.401**, with `rollForward: disable` and
`allowPrerelease: false`. Microsoft's [release metadata](https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json)
lists this SDK with the 2026-09-08 release and runtime 10.0.12. This is also the
installed SDK/runtime used for validation. The [SDK resolver policy](https://learn.microsoft.com/en-us/dotnet/core/tools/global-json)
requires the exact SDK. GitHub Actions uses setup-dotnet's
[global-json-file input](https://github.com/actions/setup-dotnet#using-the-global-json-file-input)
instead of duplicating a version range.

`Directory.Build.props` enables NuGet lock files for all six projects. The
committed files record the resolved dependency graph and content hashes;
`dotnet restore --locked-mode` rejects drift. This pins build inputs, but is not
a claim of byte-identical output across operating systems or checkout paths.
SDK and package updates must explicitly refresh and review these files.

## Package compatibility decisions

All versions below are stable. Framework compatibility computed from package
assets is distinguished from explicit vendor support; local build, test and
coverage execution supply the additional evidence for this solution.

| Package | Before | After | Evidence and reason |
| --- | --- | --- | --- |
| Microsoft.NET.Test.Sdk | 17.8.0 | 17.14.1 | [Microsoft package metadata](https://www.nuget.org/packages/Microsoft.NET.Test.Sdk/17.14.1) includes net8.0 assets and computed net10.0 compatibility. Update within major 17 refreshes the test host; [17.14.1 release notes](https://github.com/microsoft/vstest/releases/tag/v17.14.1) include protection against silently skipping unsupported targets. VSTest is preserved. |
| NUnit | 3.14.0 | unchanged | [NUnit package metadata](https://www.nuget.org/packages/NUnit/3.14.0) supplies netstandard2.0 assets, with computed net10.0 compatibility. Both regression suites pass unchanged; no NUnit 4 API migration is required. |
| NUnit3TestAdapter | 4.5.0 | unchanged | [NUnit's support matrix](https://docs.nunit.org/articles/vs-test-adapter/Supported-Frameworks.html) supports .NET 8+ with adapter 4.3.2 and later, subject to future runtime breaking changes. Discovery, filters and Explicit deep selection work with 4.5.0 on .NET 10. |
| NUnit.Analyzers | 3.9.0 | unchanged | The [official project](https://github.com/nunit/nunit.analyzers) describes Roslyn build-time analyzers, not a test runtime. The [3.9.0 package](https://www.nuget.org/packages/NUnit.Analyzers/3.9.0) is retained; Release rebuild with SDK 10.0.401 produces no analyzer load failures or diagnostics. This is local compiler compatibility evidence, not a vendor certification of this older release for .NET 10. |
| coverlet.collector | 6.0.0 | 6.0.4 | The [versioned VSTest integration guide](https://github.com/coverlet-coverage/coverlet/blob/v6.0.4/Documentation/VSTestIntegration.md) supports .NET 6+ and Test SDK 17.7.0+. The [6.0.4 patch](https://github.com/coverlet-coverage/coverlet/releases/tag/v6.0.4) fixes empty reports with include/exclude filters. Actual collection on net10.0 produced a nonempty Cobertura report. |
| BenchmarkDotNet | 0.13.12 | 0.15.8 | [0.15.0 introduced explicit .NET 10 toolchain support](https://github.com/dotnet/BenchmarkDotNet/releases/tag/v0.15.0). [0.15.8](https://github.com/dotnet/BenchmarkDotNet/releases/tag/v0.15.8) is a stable patch release in that line, including process deadlock fixes. The existing in-process benchmark executes successfully. |

The [Microsoft testing documentation](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-with-dotnet-test)
confirms VSTest remains a supported/default `dotnet test` mode in .NET 10.
`global.json` selects it explicitly. No MTP packages or runner migration were added.

The benchmark entry point now forwards explicit CLI arguments with the default
BenchmarkDotNet configuration, so `--job Dry --inProcess` performs a short check.
Without arguments it retains the original MediumRun, one launch, and
InProcessEmitToolchain configuration. Benchmark methods are unchanged.

## Validation

The baseline used a separate Microsoft SDK 8.0.425 / runtime 8.0.31 in a temporary
directory. The downloaded SDK archive was checked against the SHA-512 in
Microsoft's .NET 8 release metadata. Project files still targeted net8.0 and all
package references were unchanged during these baseline checks.

| Check | Baseline net8.0 | Migrated net10.0 |
| --- | --- | --- |
| Release solution rebuild (six projects) | Pass; 0 warnings, 0 errors | Pass; 0 warnings, 0 errors |
| Fast tests, `TestCategory!=Deep` | 851 passed; 0 failed/skipped | 851 passed; 0 failed/skipped |
| Deep perft, `TestCategory=Deep` | 16 passed; 0 failed/skipped | 16 passed; 0 failed/skipped |

The fast and deep test-name inventories in the before/after TRX files match.
The tests preserve all seven reference positions and special-move assertions.
Commands for reproducing the checks are in the [README](../README.md).

Additional .NET 10 checks:

- Locked restore succeeded, including a fresh source copy without bin/obj, a
  separate empty package cache and `--no-http-cache`; the fresh copy also built
  all six Release projects without warnings and passed the 851 fast tests.
- Coverage collection passed all 851 fast tests and produced Cobertura XML.
- `dotnet test --no-restore` from `src` built the Debug test project and passed
  851 ordinary tests without a filter or settings file; the 16 Explicit deep
  cases remained opt-in and were already verified in the dedicated Release run.
- Console displayed the initial board and accepted `e2e4`, `e7e5`, updating the
  board and side to move. It was then stopped with Ctrl+C.
- Perft completed depth 6 with 119,060,324 nodes, 2,812,008 captures and 5,248 en
  passant moves; castling and promotion counts were both zero.
- MagicBruteforce with seed 42 and one iteration completed both 64-square loops
  with exit code 0. All 128 searches reported no magic found, as expected for
  this tiny budget; this does not validate a full magic-number search.
- BenchmarkDotNet completed one Dry in-process benchmark on .NET 10.0.12. An
  earlier configuration check was interrupted when it selected MediumRun;
  the final CLI configuration selects exactly one Dry job.

Local TRX/coverage files are under ignored `artifacts/validation/`; benchmark
output is under ignored `BenchmarkDotNet.Artifacts/`. These generated outputs
are not required to build the repository.

## Limits

Validation was local on Windows x64. GitHub Actions jobs and Linux execution
were not run, and no push or merge was performed. The fresh-copy check used
the proposed working-tree files, not a published commit. The Console check
covered startup and two moves, not a full game. No full benchmark run or
comparative performance measurement was performed; a one-sample Dry result
has no useful confidence interval and supports no speedup claim. Historical
.NET 8 performance numbers in the README remain explicitly historical.
