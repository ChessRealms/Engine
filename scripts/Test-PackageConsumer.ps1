[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$PackagePath,

    [Parameter(Mandatory)]
    [string]$ExpectedPackageId,

    [Parameter(Mandatory)]
    [string]$ExpectedPackageVersion
)

$ErrorActionPreference = "Stop"

if ($ExpectedPackageId -ne "ChessRealms.Engine") {
    throw "This consumer test is scoped to package ID 'ChessRealms.Engine'; received '$ExpectedPackageId'."
}

$resolvedPackagePath = (Resolve-Path -LiteralPath $PackagePath).Path
$expectedFileName = "$ExpectedPackageId.$ExpectedPackageVersion.nupkg"
if ([System.IO.Path]::GetFileName($resolvedPackagePath) -cne $expectedFileName) {
    throw "Package filename must be '$expectedFileName', found '$([System.IO.Path]::GetFileName($resolvedPackagePath))'."
}

$packageRoot = [System.IO.Path]::GetDirectoryName($resolvedPackagePath)
$temporaryRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
$consumerRoot = [System.IO.Path]::GetFullPath(
    (Join-Path $temporaryRoot "$ExpectedPackageId.Consumer.$([guid]::NewGuid().ToString('N'))")
)
if (-not $consumerRoot.StartsWith($temporaryRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "The generated consumer path '$consumerRoot' is outside the temporary directory '$temporaryRoot'."
}

$consumerProjectName = "$ExpectedPackageId.Consumer"
$consumerProjectPath = Join-Path $consumerRoot "$consumerProjectName.csproj"
$previousPackagesPath = $env:NUGET_PACKAGES

function Invoke-DotNet {
    param(
        [Parameter(Mandatory)]
        [string[]]$Arguments,

        [Parameter(Mandatory)]
        [string]$FailureMessage
    )

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw $FailureMessage
    }
}

try {
    $env:NUGET_PACKAGES = Join-Path $consumerRoot ".packages"

    Invoke-DotNet @(
        "new", "console",
        "--name", $consumerProjectName,
        "--framework", "net10.0",
        "--output", $consumerRoot,
        "--no-restore"
    ) "Could not create the package consumer project."

    Invoke-DotNet @(
        "add", $consumerProjectPath,
        "package", $ExpectedPackageId,
        "--version", $ExpectedPackageVersion,
        "--source", $packageRoot,
        "--no-restore"
    ) "Could not add the local package reference."

    $escapedPackageRoot = [System.Security.SecurityElement]::Escape($packageRoot)
    $nugetConfig = @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local" value="$escapedPackageRoot" />
  </packageSources>
</configuration>
"@
    [System.IO.File]::WriteAllText((Join-Path $consumerRoot "NuGet.Config"), $nugetConfig)

    $program = @'
using ChessRealms.Engine;

var game = ChessGame.FromFen(ChessGame.StartingFen);
var move = CoordinateMove.Parse("e2e4");

if (!game.GetLegalMoves().Contains(move))
{
    throw new InvalidOperationException("e2e4 was not legal in the starting position.");
}

if (game.MakeMove(move) == MoveResult.None)
{
    throw new InvalidOperationException("The package consumer could not make e2e4.");
}

var piece = game.GetPiece(Square.Parse("e4"));
if (piece != new ChessPiece(PieceColor.White, PieceValue.Pawn))
{
    throw new InvalidOperationException("The white pawn was not found on e4.");
}

Span<char> fen = stackalloc char[128];
if (!game.TryFormatFen(fen, out int fenWritten) || !fen[..fenWritten].SequenceEqual(game.ToFen()))
{
    throw new InvalidOperationException("The package consumer could not format FEN into a span.");
}

if (game.PlyCount != 1 || game.GetFenAtPly(0) != ChessGame.StartingFen
    || game.GetFenAtPly(1) != game.ToFen()
    || !game.TryFormatFenAtPly(0, fen, out fenWritten)
    || !fen[..fenWritten].SequenceEqual(ChessGame.StartingFen))
{
    throw new InvalidOperationException("The package consumer could not read historical FEN.");
}

var history = game.History;

game.UndoMove();

if (game.PlyCount != 0 || game.GetFenAtPly(0) != ChessGame.StartingFen
    || history.Count != 1 || history[0].FenAfter == game.ToFen()
    || game.TryFormatFenAtPly(1, fen, out fenWritten) || fenWritten != 0)
{
    throw new InvalidOperationException("Historical FEN or snapshot ownership failed after undo.");
}

if (game.FormatSan(move) != "e4" || game.ParseSan("e4") != move)
{
    throw new InvalidOperationException("The package consumer could not format and parse SAN.");
}

Span<char> san = stackalloc char[7];
if (!game.TryFormatSan(move, san, out int written) || !san[..written].SequenceEqual("e4")
    || !game.TryParseSan(san[..written], out var parsed) || parsed != move
    || game.History.Count != 0)
{
    throw new InvalidOperationException("The package consumer could not query SAN through spans.");
}

if (game.MakeSanMove("e4+") != MoveResult.None || game.MakeSanMove("e4") == MoveResult.None
    || !game.UndoMove() || game.ToFen() != ChessGame.StartingFen)
{
    throw new InvalidOperationException("The package consumer could not apply and undo canonical SAN.");
}

game.MakeSanMove("e4");
string recordJson = GameRecord.Serialize(game.ExportRecord());
var restored = ChessGame.FromRecordJson(recordJson);
if (restored.ToFen() != game.ToFen() || restored.PlyCount != 1
    || !restored.UndoMove() || restored.ToFen() != ChessGame.StartingFen
    || game.PlyCount != 1)
{
    throw new InvalidOperationException("The package consumer could not restore record JSON with independent undo.");
}

try
{
    ChessGame.FromRecordJson(recordJson.Replace("e2e4", "e2e5"));
    throw new InvalidOperationException("Invalid record JSON did not throw.");
}
catch (System.Text.Json.JsonException exception) when (exception.InnerException is FormatException)
{
}

var document = PgnGame.FromGame(game,
    new Dictionary<string, string> { ["Event"] = "Package smoke test" }, PgnResult.WhiteWin);
using var writer = new StringWriter();
document.WriteTo(writer);
if (writer.ToString() != document.ToPgn()
    || !PgnGame.TryParse(writer.ToString().AsSpan(), out var imported, out var diagnostic)
    || diagnostic is not null || imported.Result != PgnResult.WhiteWin
    || imported.Tags["Event"] != "Package smoke test"
    || imported.Moves.Count != 1 || imported.ToGame().ToFen() != game.ToFen()
    || imported.ToGame().IsFinished)
{
    throw new InvalidOperationException("The package consumer could not round-trip PGN.");
}

var directPgn = PgnGame.Parse(writer.ToString().AsSpan(), out ChessGame pgnReplay);
if (directPgn.ToPgn() != imported.ToPgn() || pgnReplay.ToFen() != game.ToFen()
    || pgnReplay.IsFinished || directPgn.Result != PgnResult.WhiteWin
    || !PgnGame.TryParse(writer.ToString().AsSpan(), out var tryPgn, out var tryPgnReplay, out var tryPgnError)
    || tryPgnError is not null || tryPgn.ToPgn() != directPgn.ToPgn()
    || tryPgnReplay.ToFen() != pgnReplay.ToFen())
{
    throw new InvalidOperationException("The package consumer could not import PGN with its validated game.");
}

if (!pgnReplay.UndoMove() || directPgn.Moves.Count != 1 || tryPgnReplay.PlyCount != 1)
{
    throw new InvalidOperationException("The direct PGN document and games are not independent.");
}

if (PgnGame.TryParse("1. e4 * trailing", out var failedPgn, out var failedPgnGame, out var directError)
    || failedPgn is not null || failedPgnGame is not null || directError?.Code != PgnErrorCode.UnexpectedToken)
{
    throw new InvalidOperationException("The direct PGN import exposed partial outputs.");
}

if (PgnGame.TryParse("1. e4 {comment} *", out var invalid, out var error)
    || invalid is not null || error?.Code != PgnErrorCode.UnsupportedAnnotation || error.Offset != 6)
{
    throw new InvalidOperationException("The package consumer could not inspect a PGN diagnostic.");
}

try
{
    PgnGame.Parse("1. e5 *");
    throw new InvalidOperationException("Invalid PGN did not throw.");
}
catch (PgnParseException exception) when (exception.Diagnostic.Code == PgnErrorCode.InvalidSan)
{
}

try
{
    PgnGame.Parse("1. e5 *", out _);
    throw new InvalidOperationException("Invalid direct PGN import did not throw.");
}
catch (PgnParseException exception) when (exception.Diagnostic.Code == PgnErrorCode.InvalidSan)
{
}
'@
    [System.IO.File]::WriteAllText((Join-Path $consumerRoot "Program.cs"), $program)

    Invoke-DotNet @(
        "restore", $consumerProjectPath,
        "--configfile", (Join-Path $consumerRoot "NuGet.Config")
    ) "Package consumer restore failed."

    Invoke-DotNet @(
        "run",
        "--project", $consumerProjectPath,
        "--configuration", "Release",
        "--no-restore"
    ) "Package consumer smoke test failed."

    Write-Host "Verified $expectedFileName through an isolated package consumer."
}
finally {
    $env:NUGET_PACKAGES = $previousPackagesPath
    if ($consumerRoot.StartsWith($temporaryRoot, [System.StringComparison]::OrdinalIgnoreCase) -and
        (Test-Path -LiteralPath $consumerRoot)) {
        Remove-Item -LiteralPath $consumerRoot -Recurse -Force
    }
}
