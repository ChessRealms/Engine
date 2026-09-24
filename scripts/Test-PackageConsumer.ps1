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

game.UndoMove();
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
