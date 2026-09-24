[CmdletBinding()]
param(
    [string]$ProjectPath = "src/ChessRealms.Engine/ChessRealms.Engine.csproj",

    [Parameter(Mandatory)]
    [ValidateSet("push", "workflow_dispatch")]
    [string]$EventName,

    [string]$GitRef,

    [string]$GitRefName,

    [AllowEmptyString()]
    [string]$DispatchVersion = "",

    [string]$MainBranch = "main",

    [string]$GitHubOutputPath
)

$ErrorActionPreference = "Stop"

$resolvedProjectPath = (Resolve-Path -LiteralPath $ProjectPath).Path
[xml]$project = Get-Content -LiteralPath $resolvedProjectPath -Raw
$versionPrefix = [string]$project.Project.PropertyGroup.VersionPrefix
$corePattern = '(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)'
$packagePattern = "^(?<core>$corePattern)(-(?<label>alpha|beta|rc)\.(0|[1-9][0-9]*))?$"

if ($EventName -eq "push") {
    if ($GitRef -notmatch '^refs/tags/v(?<version>.+)$') {
        throw "A production release must be triggered by a v-prefixed tag."
    }

    $packageVersion = $Matches.version
    $versionMatch = [regex]::Match($packageVersion, $packagePattern)
    if (-not $versionMatch.Success) {
        throw "Tag '$GitRefName' is not an accepted release tag. Use vMAJOR.MINOR.PATCH with an optional -alpha.N, -beta.N, or -rc.N suffix."
    }

    $stableCore = $versionMatch.Groups['core'].Value
    $remoteMainRef = "refs/remotes/origin/$MainBranch"
    git fetch --no-tags origin "${MainBranch}:$remoteMainRef"
    if ($LASTEXITCODE -ne 0) {
        throw "Could not fetch origin/$MainBranch for release ancestry validation."
    }

    $tagCommit = (git rev-list -n 1 $GitRef).Trim()
    if ($LASTEXITCODE -ne 0 -or -not $tagCommit) {
        throw "Could not resolve the release tag commit."
    }

    git merge-base --is-ancestor $tagCommit $remoteMainRef
    if ($LASTEXITCODE -ne 0) {
        throw "Tag commit $tagCommit is not reachable from origin/$MainBranch."
    }
}
else {
    $packageVersion = $DispatchVersion.Trim()
    if (-not $packageVersion) {
        $packageVersion = $versionPrefix
    }

    $versionMatch = [regex]::Match($packageVersion, $packagePattern)
    if (-not $versionMatch.Success) {
        throw "Dry-run version '$packageVersion' is not accepted. Use MAJOR.MINOR.PATCH with an optional -alpha.N, -beta.N, or -rc.N suffix (without v)."
    }

    $stableCore = $versionMatch.Groups['core'].Value
    Write-Host "Manual dispatch is a dry run: publishing and GitHub Release creation are disabled."
}

if ($versionPrefix -notmatch "^$corePattern$") {
    throw "Project VersionPrefix '$versionPrefix' must be a stable MAJOR.MINOR.PATCH version."
}

if ($stableCore -cne $versionPrefix) {
    throw "Release version core '$stableCore' does not match project VersionPrefix '$versionPrefix'."
}

if ($GitHubOutputPath) {
    Add-Content -LiteralPath $GitHubOutputPath -Value "package_version=$packageVersion"
    Add-Content -LiteralPath $GitHubOutputPath -Value "is_prerelease=$($packageVersion.Contains('-').ToString().ToLowerInvariant())"
}

Write-Host "Validated package version $packageVersion against VersionPrefix $versionPrefix."
