#requires -Version 7.0
<#
Checks the SLNX solution against tracked and untracked, nonignored repository files.
Run with: pwsh -NoProfile -File scripts/Verify-SolutionStructure.ps1
No files are changed. Git must be available on PATH.
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$solutionPath = Join-Path $repositoryRoot 'ChessRealms.Engine.slnx'
$issues = [Collections.Generic.List[string]]::new()

function Normalize-RelativePath([string] $Path) {
    # Normalize separators only: spelling and hierarchy must remain exact.
    return $Path.Replace('\', '/')
}

function Get-ParentPath([string] $Path) {
    $separator = $Path.LastIndexOf('/')
    if ($separator -lt 0) { return '' }
    return $Path.Substring(0, $separator)
}

function Get-PhysicalPath([string] $Path) {
    if ($Path -match '^[/\\]|^[A-Za-z]:' -or
        @($Path.Split('/') | Where-Object { $_ -in @('', '.', '..') }).Count -gt 0) {
        $issues.Add("Invalid repository-relative path: '$Path'.")
        return $null
    }

    # Test-Path alone cannot detect casing errors on a case-insensitive volume.
    $current = $repositoryRoot
    $parts = [Collections.Generic.List[string]]::new()
    foreach ($part in $Path.Split('/')) {
        if (-not [IO.Directory]::Exists($current)) { return $null }
        $entries = @([IO.Directory]::EnumerateFileSystemEntries($current))
        $entry = @($entries | Where-Object { [IO.Path]::GetFileName($_) -ceq $part })
        if ($entry.Count -eq 0) {
            $entry = @($entries | Where-Object { [IO.Path]::GetFileName($_) -ieq $part })
        }
        if ($entry.Count -ne 1) { return $null }
        $current = $entry[0]
        $parts.Add([IO.Path]::GetFileName($current))
    }
    if (-not [IO.File]::Exists($current)) { return $null }
    return $parts -join '/'
}

function Add-FolderAncestors([string] $Path) {
    while ($Path) {
        [void] $expectedFolders.Add($Path)
        $Path = Get-ParentPath $Path
    }
}

function Compare-References($Expected, $Actual, [string] $Kind) {
    $seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($reference in $Actual) {
        $path = $reference.Path
        if (-not $seen.Add($path)) { $issues.Add("Duplicate ${Kind}: '$path'.") }
        if (-not $Expected.ContainsKey($path)) {
            $caseMatch = @($Expected.Keys | Where-Object { $_ -ieq $path })
            if ($caseMatch.Count -gt 0) {
                $issues.Add("Case mismatch in ${Kind}: '$path'; expected '$($caseMatch -join "', '")'.")
            } else {
                $issues.Add("Extra ${Kind}: '$path'.")
            }
        } elseif ($reference.Folder -cne $Expected[$path]) {
            $issues.Add("Misplaced ${Kind}: '$path' is in '$($reference.Folder)'; expected '$($Expected[$path])'.")
        }
        $physicalPath = Get-PhysicalPath $path
        if ($null -eq $physicalPath) {
            $issues.Add("Nonexistent file for ${Kind}: '$path'.")
        } elseif ($physicalPath -cne $path) {
            $issues.Add("Physical path case mismatch: '$path'; on disk '$physicalPath'.")
        }
    }
    foreach ($path in $Expected.Keys) {
        if (@($Actual | Where-Object { $_.Path -ceq $path }).Count -eq 0) {
            $issues.Add("Missing ${Kind}: '$path' in '$($Expected[$path])'.")
        }
    }
}

try {
    if ([IO.File]::Exists((Join-Path $repositoryRoot 'ChessRealms.Engine.sln'))) {
        $issues.Add('Legacy .sln remains beside .slnx; keep a single root solution for CLI discovery.')
    }
    # NUL-delimited output preserves spaces, Unicode and Git's otherwise quoted names.
    $git = [Diagnostics.ProcessStartInfo]::new('git')
    $git.WorkingDirectory = $repositoryRoot
    $git.UseShellExecute = $false
    $git.RedirectStandardOutput = $true
    $git.StandardOutputEncoding = [Text.UTF8Encoding]::new($false)
    foreach ($argument in @('ls-files', '--cached', '--others', '--exclude-standard', '-z')) {
        $git.ArgumentList.Add($argument)
    }
    $process = [Diagnostics.Process]::Start($git)
    $output = $process.StandardOutput.ReadToEnd()
    $process.WaitForExit()
    $gitExitCode = $process.ExitCode
    $process.Dispose()
    if ($gitExitCode -ne 0) { throw "git ls-files failed (exit $gitExitCode). Run inside a Git checkout." }

    $expectedItems = [Collections.Generic.Dictionary[string, string]]::new([StringComparer]::Ordinal)
    $expectedProjects = [Collections.Generic.Dictionary[string, string]]::new([StringComparer]::Ordinal)
    $expectedFolders = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $gitPaths = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($gitPath in $output.Split([char]0, [StringSplitOptions]::RemoveEmptyEntries)) {
        if (-not $gitPaths.Add($gitPath)) { continue }
        if ($gitPath -notmatch '^(docs|scripts)/' -and $gitPath -notmatch '^src/.*\.csproj$') { continue }
        $path = Get-PhysicalPath $gitPath
        if ($null -eq $path) {
            $issues.Add("Repository file is missing on disk: '$gitPath' (still listed by Git).")
            $path = $gitPath
        } elseif ($path -cne $gitPath) {
            $issues.Add("Git path case mismatch: '$gitPath'; on disk '$path'. Record case-only renames in Git.")
        }
        if ($path -cmatch '^(docs|scripts)/') {
            $parent = Get-ParentPath $path
            $expectedItems[$path] = $parent
        } elseif ($path -cmatch '^src/' -and $path -imatch '\.csproj$') {
            # A project represents its own directory. Mirror only grouping directories
            # above it: src/group/Project/Project.csproj belongs to src/group.
            $directory = Get-ParentPath $path
            $parent = if ($directory -ceq 'src') { 'src' } else { Get-ParentPath $directory }
            $expectedProjects[$path] = $parent
        } else {
            $issues.Add("Repository root folder case mismatch: '$path'; use docs, scripts or src.")
            continue
        }
        Add-FolderAncestors $parent
    }

    # SLNX represents nested folders by absolute names such as /docs/guides/.
    # XML Folder elements are siblings, and their Project/File children are direct.
    $settings = [Xml.XmlReaderSettings]::new()
    $settings.DtdProcessing = [Xml.DtdProcessing]::Prohibit
    $settings.XmlResolver = $null
    $reader = [Xml.XmlReader]::Create($solutionPath, $settings)
    $document = [Xml.XmlDocument]::new()
    $document.XmlResolver = $null
    try { $document.Load($reader) } finally { $reader.Dispose() }
    if ($document.DocumentElement.LocalName -cne 'Solution' -or $document.DocumentElement.NamespaceURI) {
        throw 'Expected an SLNX <Solution> root without an XML namespace.'
    }

    $items = [Collections.Generic.List[object]]::new()
    $projectReferences = [Collections.Generic.List[object]]::new()
    $actualFolders = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $declaredFolders = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $ids = [Collections.Generic.HashSet[guid]]::new()
    foreach ($node in $document.SelectNodes('//*')) {
        if ($node.HasAttribute('Id')) {
            $id = [guid]::Empty
            if (-not [guid]::TryParse($node.GetAttribute('Id'), [ref] $id) -or $id -eq [guid]::Empty) {
                $issues.Add("Invalid solution item GUID: '$($node.GetAttribute('Id'))'.")
            } elseif (-not $ids.Add($id)) {
                $issues.Add("Duplicate project/folder GUID: $id.")
            }
        }
        if ($node.LocalName -in @('Folder', 'Project', 'File') -and
            ($node.LocalName -cnotin @('Folder', 'Project', 'File') -or $node.NamespaceURI)) {
            $issues.Add("Invalid SLNX element name: '$($node.LocalName)'.")
        }
    }
    foreach ($folder in $document.SelectNodes('//Folder')) {
        if ($folder.ParentNode -ne $document.DocumentElement) {
            $issues.Add('Folder elements must be direct children of Solution; use full names such as /docs/guides/.')
        }
        $name = Normalize-RelativePath $folder.GetAttribute('Name')
        if ($name -notmatch '^/[^/]+(?:/[^/]+)*/$' -or
            @($name.Trim('/').Split('/') | Where-Object { $_ -in @('.', '..') }).Count -gt 0) {
            $issues.Add("Invalid solution folder path: '$name'. Expected /folder/ or /folder/subfolder/.")
        }
        $path = $name.Trim('/')
        if (-not $declaredFolders.Add($path)) { $issues.Add("Duplicate solution folder: '$path'.") }
        # SLNX permits implicit ancestors: /docs/guides/ also creates /docs/.
        while ($path) {
            [void] $actualFolders.Add($path)
            $path = Get-ParentPath $path
        }
    }
    foreach ($node in $document.SelectNodes('//File | //Project')) {
        $parent = $node.ParentNode
        $folder = ''
        if ($parent.LocalName -ceq 'Folder') {
            $folder = (Normalize-RelativePath $parent.GetAttribute('Name')).Trim('/')
        } elseif ($node.LocalName -ceq 'File' -or $parent -ne $document.DocumentElement) {
            $issues.Add("Invalid placement of SLNX $($node.LocalName) element: '$($node.GetAttribute('Path'))'.")
        }
        $path = Normalize-RelativePath $node.GetAttribute('Path')
        $reference = [pscustomobject]@{ Path = $path; Folder = $folder }
        if ($node.LocalName -ceq 'File') { $items.Add($reference) } else { $projectReferences.Add($reference) }
    }
    foreach ($path in $actualFolders) {
        if (-not $expectedFolders.Contains($path)) { $issues.Add("Extra or incorrectly cased solution folder: '$path'.") }
    }
    foreach ($path in $expectedFolders) {
        if (-not $actualFolders.Contains($path)) { $issues.Add("Missing solution folder: '$path'.") }
    }
    Compare-References $expectedItems $items 'solution item'
    Compare-References $expectedProjects $projectReferences 'project reference'
} catch {
    $issues.Add("Verification failed: $($_.Exception.Message)")
}

if ($issues.Count -gt 0) {
    Write-Output "Solution structure check failed ($($issues.Count) discrepancies):"
    foreach ($issue in $issues | Sort-Object -Unique) { Write-Output " - $issue" }
    exit 1
}
Write-Output "Solution structure verified: $($expectedProjects.Count) projects, $($expectedItems.Count) solution items, $($expectedFolders.Count) folders."
exit 0
