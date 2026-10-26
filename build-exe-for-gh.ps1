<#
.SYNOPSIS
Builds Text Grab's unpackaged downloads and uploads them to a draft GitHub release.
.EXAMPLE
.\build-exe-for-gh.ps1
.EXAMPLE
.\build-exe-for-gh.ps1 -SkipRelease
.EXAMPLE
.\build-exe-for-gh.ps1 -Prerelease -Notes 'Release notes'
#>
[CmdletBinding()]
param(
    [string]$Notes,
    [switch]$Prerelease,
    [switch]$SkipRelease,

    # Uncommitted changes will be built but will not be included in the tag.
    [switch]$AllowDirty
)

$ErrorActionPreference = 'Stop'
$RepoRoot = $PSScriptRoot
$ProjectPath = Join-Path $RepoRoot 'Text-Grab\Text-Grab.csproj'
$BuildScript = Join-Path $RepoRoot 'build-unpackaged.ps1'
$ArtifactRoot = Join-Path $RepoRoot 'bld\artifacts'

function Invoke-Git {
    param([Parameter(Mandatory)][string[]]$GitArguments)

    $Output = & git -C $RepoRoot @GitArguments
    if ($LASTEXITCODE -ne 0) {
        throw "git $($GitArguments -join ' ') failed with exit code $LASTEXITCODE."
    }
    return $Output
}

function Assert-CleanWorktree {
    if (-not $AllowDirty) {
        $Status = Invoke-Git -GitArguments @('status', '--porcelain')
        if ($Status) {
            throw "Working tree has uncommitted changes; commit/stash them or pass -AllowDirty.`n$($Status -join "`n")"
        }
    }
}

if ([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT) {
    throw 'Building Text Grab requires Windows.'
}

$RequiredTools = @('dotnet')
if (-not $SkipRelease) {
    $RequiredTools += 'git', 'gh'
}
foreach ($Tool in $RequiredTools) {
    if (-not (Get-Command $Tool -ErrorAction SilentlyContinue)) {
        throw "$Tool is required but was not found on PATH."
    }
}
foreach ($Path in $ProjectPath, $BuildScript) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Required file not found: $Path"
    }
}

$VersionOutput = & dotnet msbuild $ProjectPath -nologo -property:Configuration=Release -getProperty:Version
if ($LASTEXITCODE -ne 0) {
    throw 'Could not read the app version from Text-Grab.csproj.'
}
$Version = ($VersionOutput | Out-String).Trim()
if ($Version -notmatch '^\d+\.\d+\.\d+(?:\.\d+)?(?:-[0-9A-Za-z.-]+)?(?:\+[0-9A-Za-z.-]+)?$') {
    throw "Invalid app version returned by MSBuild: '$Version'."
}
$Tag = "v$Version"

Write-Host "Building Text Grab $Version..." -ForegroundColor Green

if (-not $SkipRelease) {
    & gh auth status
    if ($LASTEXITCODE -ne 0) {
        throw 'gh is not authenticated. Run "gh auth login" first.'
    }

    $OriginUrl = Invoke-Git -GitArguments @('remote', 'get-url', 'origin')
    if ($OriginUrl -notmatch 'github\.com[:/](?<repo>[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+?)(?:\.git)?/?$') {
        throw "Could not determine the GitHub owner/repository from origin ($OriginUrl)."
    }
    $Repository = $Matches['repo']
    $Commit = Invoke-Git -GitArguments @('rev-parse', '--verify', 'HEAD')
    Assert-CleanWorktree

    Invoke-Git -GitArguments @('fetch', 'origin', '--quiet') | Out-Null
    $RemoteBranches = Invoke-Git -GitArguments @('branch', '--remotes', '--contains', $Commit, '--list', 'origin/*')
    if (-not $RemoteBranches) {
        throw 'HEAD has not been pushed to a branch on origin. Push before releasing.'
    }

    $LocalTag = Invoke-Git -GitArguments @('tag', '--list', $Tag)
    $RemoteTag = Invoke-Git -GitArguments @('ls-remote', '--tags', 'origin', "refs/tags/$Tag")
    if ($LocalTag -or $RemoteTag) {
        throw "Tag $Tag already exists. Bump Version in Text-Grab.csproj before releasing."
    }

    # Include drafts and all pages, not just the most recent published releases.
    $ReleaseTags = & gh api "repos/$Repository/releases" --paginate --jq '.[].tag_name'
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not check existing GitHub releases.'
    }
    if ($Tag -in $ReleaseTags) {
        throw "A release for $Tag already exists on GitHub (possibly a draft). Delete it or bump Version."
    }
}

# Beta selects ZIP output; it does not change the app version or release status.
& $BuildScript -Routine Beta

$AssetNames = @(
    'Text-Grab.exe'
    'Text-Grab-arm64.exe'
    'Text-Grab-x64-Self-Contained.zip'
    'Text-Grab-arm64-Self-Contained.zip'
)
$Assets = foreach ($Name in $AssetNames) {
    $Path = Join-Path $ArtifactRoot $Name
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Expected release artifact not found: $Path"
    }
    $File = Get-Item -LiteralPath $Path
    if ($File.Length -eq 0) {
        throw "Release artifact is empty: $Path"
    }
    $File
}

if ($SkipRelease) {
    Write-Host "Build completed. Artifacts are in: $ArtifactRoot" -ForegroundColor Green
    return
}

if ((Invoke-Git -GitArguments @('rev-parse', '--verify', 'HEAD')) -ne $Commit) {
    throw 'HEAD changed during the build. No tag or release was created; rebuild from the intended commit.'
}
Assert-CleanWorktree

Write-Host "Creating local tag $Tag at $Commit..."
Invoke-Git -GitArguments @('tag', $Tag, $Commit) | Out-Null

$GhArguments = @(
    'release', 'create', $Tag
    '--repo', $Repository
    '--title', $Tag
    '--target', $Commit
    '--draft'
) + $Assets.FullName

$NotesFile = $null
try {
    if ($Notes) {
        $NotesFile = [System.IO.Path]::GetTempFileName()
        [System.IO.File]::WriteAllText($NotesFile, $Notes, [System.Text.UTF8Encoding]::new($false))
        $GhArguments += '--notes-file', $NotesFile
    }
    else {
        $GhArguments += '--generate-notes'
    }
    if ($Prerelease) {
        $GhArguments += '--prerelease'
    }

    Write-Host "Creating draft GitHub release $Tag..."
    & gh @GhArguments
    if ($LASTEXITCODE -ne 0) {
        throw "Release creation/upload failed. Check GitHub for a partial draft and remove it and the local tag before retrying (git tag -d $Tag)."
    }
}
finally {
    if ($NotesFile) {
        Remove-Item -LiteralPath $NotesFile -Force
    }
}

Write-Host "Draft release $Tag is ready with all four artifacts. Publishing it on GitHub will create the remote tag at $Commit." -ForegroundColor Green
