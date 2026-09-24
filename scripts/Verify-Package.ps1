[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$PackagePath,

    [Parameter(Mandatory)]
    [string]$ExpectedPackageId,

    [Parameter(Mandatory)]
    [string]$ExpectedPackageVersion,

    [string]$SymbolPackagePath
)

$ErrorActionPreference = "Stop"

if ($ExpectedPackageId -ne "ChessRealms.Engine") {
    throw "This verifier is scoped to package ID 'ChessRealms.Engine'; received '$ExpectedPackageId'."
}

$resolvedPackagePath = (Resolve-Path -LiteralPath $PackagePath).Path
if (-not $SymbolPackagePath) {
    $SymbolPackagePath = [System.IO.Path]::ChangeExtension($resolvedPackagePath, ".snupkg")
}
$resolvedSymbolPackagePath = (Resolve-Path -LiteralPath $SymbolPackagePath).Path

$expectedPackageFileName = "$ExpectedPackageId.$ExpectedPackageVersion.nupkg"
$expectedSymbolPackageFileName = "$ExpectedPackageId.$ExpectedPackageVersion.snupkg"

if ([System.IO.Path]::GetFileName($resolvedPackagePath) -cne $expectedPackageFileName) {
    throw "Package filename must be '$expectedPackageFileName', found '$([System.IO.Path]::GetFileName($resolvedPackagePath))'."
}

if ([System.IO.Path]::GetFileName($resolvedSymbolPackagePath) -cne $expectedSymbolPackageFileName) {
    throw "Symbol package filename must be '$expectedSymbolPackageFileName', found '$([System.IO.Path]::GetFileName($resolvedSymbolPackagePath))'."
}

Add-Type -AssemblyName System.IO.Compression.FileSystem

function Get-PackageData {
    param(
        [Parameter(Mandatory)]
        [string]$Path
    )

    $archive = [System.IO.Compression.ZipFile]::OpenRead($Path)
    try {
        $entries = @($archive.Entries | ForEach-Object { $_.FullName.Replace('\', '/') })
        $nuspecEntries = @($archive.Entries | Where-Object { $_.FullName -like "*.nuspec" })
        if ($nuspecEntries.Count -ne 1) {
            throw "Package '$Path' must contain exactly one .nuspec file; found $($nuspecEntries.Count)."
        }

        $reader = [System.IO.StreamReader]::new($nuspecEntries[0].Open())
        try {
            [xml]$nuspec = $reader.ReadToEnd()
        }
        finally {
            $reader.Dispose()
        }

        return @{
            Entries = $entries
            Nuspec = $nuspec
        }
    }
    finally {
        $archive.Dispose()
    }
}

function Get-MetadataValue {
    param(
        [Parameter(Mandatory)]
        [xml]$Nuspec,

        [Parameter(Mandatory)]
        [string]$ElementName
    )

    $node = $Nuspec.SelectSingleNode("/*[local-name()='package']/*[local-name()='metadata']/*[local-name()='$ElementName']")
    if ($null -eq $node) {
        return $null
    }

    return $node.InnerText
}

function Assert-Equal {
    param(
        [Parameter(Mandatory)]
        [string]$Description,

        [AllowNull()]
        [string]$Actual,

        [Parameter(Mandatory)]
        [string]$Expected
    )

    if ($Actual -cne $Expected) {
        throw "$Description must be '$Expected', found '$Actual'."
    }
}

function Assert-Identity {
    param(
        [Parameter(Mandatory)]
        [xml]$Nuspec,

        [Parameter(Mandatory)]
        [string]$Path
    )

    Assert-Equal "Package ID in '$Path'" (Get-MetadataValue $Nuspec "id") $ExpectedPackageId
    Assert-Equal "Package version in '$Path'" (Get-MetadataValue $Nuspec "version") $ExpectedPackageVersion
}

$package = Get-PackageData $resolvedPackagePath
$symbols = Get-PackageData $resolvedSymbolPackagePath

Assert-Identity $package.Nuspec $resolvedPackagePath
Assert-Identity $symbols.Nuspec $resolvedSymbolPackagePath

Assert-Equal "Package title" (Get-MetadataValue $package.Nuspec "title") "ChessRealms.Engine"
Assert-Equal "Package description" (Get-MetadataValue $package.Nuspec "description") "A reusable .NET library for standard chess positions, legal move generation, game state, history, undo, FEN, and standard game-ending rules."
Assert-Equal "Package authors" (Get-MetadataValue $package.Nuspec "authors") "ChessRealms"
Assert-Equal "Package project URL" (Get-MetadataValue $package.Nuspec "projectUrl") "https://github.com/ChessRealms/Engine"
Assert-Equal "Package README" (Get-MetadataValue $package.Nuspec "readme") "README.md"

$license = $package.Nuspec.SelectSingleNode("/*[local-name()='package']/*[local-name()='metadata']/*[local-name()='license']")
if ($null -eq $license -or $license.GetAttribute("type") -cne "expression" -or $license.InnerText -cne "MIT") {
    throw "Package license must be the MIT SPDX expression."
}

$repository = $package.Nuspec.SelectSingleNode("/*[local-name()='package']/*[local-name()='metadata']/*[local-name()='repository']")
if ($null -eq $repository -or
    $repository.GetAttribute("type") -cne "git" -or
    $repository.GetAttribute("url") -cne "https://github.com/ChessRealms/Engine.git") {
    throw "Package repository must be the ChessRealms/Engine Git repository with type 'git'."
}

$expectedPackageEntries = @(
    "_rels/.rels"
    "$ExpectedPackageId.nuspec"
    "LICENSE.txt"
    "README.md"
    "lib/net10.0/$ExpectedPackageId.dll"
    "lib/net10.0/$ExpectedPackageId.xml"
    "[Content_Types].xml"
    "package/services/metadata/core-properties/nuget.psmdcp"
)
$unexpectedPackageEntries = @($package.Entries | Where-Object { $_ -notin $expectedPackageEntries })
$missingPackageEntries = @($expectedPackageEntries | Where-Object { $_ -notin $package.Entries })

if ($missingPackageEntries.Count -gt 0) {
    throw "Package is missing required entries:`n- $($missingPackageEntries -join "`n- ")"
}

if ($unexpectedPackageEntries.Count -gt 0) {
    throw "Package contains unexpected entries (including any console, test, Perft, benchmark, magic-bruteforce, or source implementation content):`n- $($unexpectedPackageEntries -join "`n- ")"
}

$expectedSymbolEntries = @(
    "_rels/.rels"
    "$ExpectedPackageId.nuspec"
    "lib/net10.0/$ExpectedPackageId.pdb"
    "[Content_Types].xml"
    "package/services/metadata/core-properties/nuget.psmdcp"
)
$unexpectedSymbolEntries = @($symbols.Entries | Where-Object { $_ -notin $expectedSymbolEntries })
$missingSymbolEntries = @($expectedSymbolEntries | Where-Object { $_ -notin $symbols.Entries })

if ($missingSymbolEntries.Count -gt 0) {
    throw "Symbol package is missing required entries:`n- $($missingSymbolEntries -join "`n- ")"
}

if ($unexpectedSymbolEntries.Count -gt 0) {
    throw "Symbol package contains unexpected entries:`n- $($unexpectedSymbolEntries -join "`n- ")"
}

Write-Host "Verified $expectedPackageFileName and $expectedSymbolPackageFileName."
