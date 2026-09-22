$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$solutionPath = Join-Path $repositoryRoot "ChessRealms.Engine.slnx"

[xml]$solution = Get-Content $solutionPath -Raw

$itemGroups = @(
    @{
        DisplayName = "documentation"
        Path = "docs"
        Include = { param($file) $file.Extension -eq ".md" }
    },
    @{
        DisplayName = "scripts"
        Path = "scripts"
        Include = { param($file) $true }
    }
)

$failures = [System.Collections.Generic.List[string]]::new()

foreach ($itemGroup in $itemGroups) {
    $groupPath = $itemGroup.Path
    $groupRoot = Join-Path $repositoryRoot $groupPath
    $include = $itemGroup.Include
    $solutionNodes = @(
        $solution.SelectNodes("//File[starts-with(@Path, '$groupPath/')]")
    )
    $solutionPaths = @(
        $solutionNodes |
            ForEach-Object { $_.Path.Replace('\', '/') } |
            Sort-Object -Unique
    )
    $fileSystemPaths = @(
        Get-ChildItem $groupRoot -Recurse -File |
            Where-Object { & $include $_ } |
            ForEach-Object {
                [System.IO.Path]::GetRelativePath($repositoryRoot, $_.FullName).Replace('\', '/')
            } |
            Sort-Object -Unique
    )

    $missingFromSolution = @(
        $fileSystemPaths | Where-Object { $_ -notin $solutionPaths }
    )
    $missingFromFileSystem = @(
        $solutionPaths | Where-Object { $_ -notin $fileSystemPaths }
    )
    $incorrectFolders = @(
        $solutionNodes | ForEach-Object {
            $relativePath = $_.Path.Replace('\', '/')
            $directory = [System.IO.Path]::GetDirectoryName($relativePath).Replace('\', '/')
            $expectedFolder = "/$directory/"
            $actualFolder = $_.ParentNode.GetAttribute("Name")

            if ($actualFolder -ne $expectedFolder) {
                "$relativePath (expected $expectedFolder, found $actualFolder)"
            }
        }
    )

    if ($missingFromSolution.Count -gt 0) {
        $failures.Add(
            "$($itemGroup.DisplayName) files missing from ChessRealms.Engine.slnx:`n- " +
            ($missingFromSolution -join "`n- ")
        )
    }

    if ($missingFromFileSystem.Count -gt 0) {
        $failures.Add(
            "ChessRealms.Engine.slnx references missing $($itemGroup.DisplayName) files:`n- " +
            ($missingFromFileSystem -join "`n- ")
        )
    }

    if ($incorrectFolders.Count -gt 0) {
        $failures.Add(
            "$($itemGroup.DisplayName) files placed in incorrect solution folders:`n- " +
            ($incorrectFolders -join "`n- ")
        )
    }
}

if ($failures.Count -gt 0) {
    Write-Error ($failures -join "`n`n")
}

Write-Host "Solution items match the docs and scripts filesystem structure."
