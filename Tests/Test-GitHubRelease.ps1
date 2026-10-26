[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$SourceRoot = Split-Path -Parent $PSScriptRoot
$FixtureRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('Text Grab release test ' + [guid]::NewGuid().ToString('N'))
$ArtifactRoot = Join-Path $FixtureRoot 'bld\artifacts'
$Calls = [System.Collections.Generic.List[object]]::new()
$Scenario = @{}
$Commit = '0123456789abcdef0123456789abcdef01234567'
$global:TextGrabReleaseTestState = @{
    Calls = $Calls
    Scenario = $Scenario
    Commit = $Commit
    ArtifactRoot = $ArtifactRoot
}

function Assert-True {
    param([bool]$Condition, [string]$Message)

    if (-not $Condition) { throw $Message }
}

# These functions shadow native tools only in this test script's scope.
function dotnet {
    $Arguments = @($args)
    $State = $global:TextGrabReleaseTestState
    $State.Calls.Add([pscustomobject]@{ Tool = 'dotnet'; Arguments = $Arguments })
    $global:LASTEXITCODE = 0
    if ($Arguments[0] -eq 'msbuild') {
        $State.Scenario.Version
        return
    }
    if ($Arguments[0] -ne 'publish') { throw "Unexpected dotnet command: $Arguments" }
    if ($State.Scenario.BuildFails) {
        $global:LASTEXITCODE = 1
        return
    }

    $OutputPath = $Arguments[[array]::IndexOf($Arguments, '--output') + 1]
    New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null
    Set-Content -LiteralPath (Join-Path $OutputPath 'Text-Grab.exe') -Value 'Executable fixture'
    if ('--self-contained' -in $Arguments) {
        Set-Content -LiteralPath (Join-Path $OutputPath 'runtime.dll') -Value 'Runtime fixture'
    }
    Set-Content -LiteralPath (Join-Path $State.ArtifactRoot 'do-not-upload.pdb') -Value 'Unrelated artifact'
}

function git {
    $Arguments = @($args)[2..($args.Count - 1)]
    $State = $global:TextGrabReleaseTestState
    $State.Calls.Add([pscustomobject]@{ Tool = 'git'; Arguments = $Arguments })
    $global:LASTEXITCODE = 0
    switch ($Arguments[0]) {
        'remote' { 'git@github.com:TheJoeFin/Text-Grab.git' }
        'rev-parse' {
            $State.Scenario.HeadReads++
            if ($State.Scenario.HeadChanges -and $State.Scenario.HeadReads -gt 1) {
                'fedcba9876543210fedcba9876543210fedcba98'
            }
            else { $State.Commit }
        }
        'status' {
            if ($State.Scenario.Dirty) { ' M Text-Grab\Text-Grab.csproj' }
        }
        'fetch' {
            if ($State.Scenario.FetchFails) { $global:LASTEXITCODE = 1 }
        }
        'branch' {
            if ($State.Scenario.Pushed) { '  origin/main' }
        }
        'tag' {
            if ($Arguments[1] -eq '--list' -and $State.Scenario.LocalTag) {
                "v$($State.Scenario.Version)"
            }
        }
        'ls-remote' {
            if ($State.Scenario.RemoteTag) { "$($State.Commit)`trefs/tags/v$($State.Scenario.Version)" }
        }
        default { throw "Unexpected git command: $Arguments" }
    }
}

function gh {
    $Arguments = @($args)
    $Call = [pscustomobject]@{ Tool = 'gh'; Arguments = $Arguments; Notes = $null; NotesFile = $null }
    $State = $global:TextGrabReleaseTestState
    $State.Calls.Add($Call)
    $global:LASTEXITCODE = 0
    switch ($Arguments[0]) {
        'auth' {
            if ($State.Scenario.AuthFails) { $global:LASTEXITCODE = 1 }
        }
        'api' {
            if ($State.Scenario.ApiFails) { $global:LASTEXITCODE = 1 }
            elseif ($State.Scenario.ExistingRelease) { "v$($State.Scenario.Version)" }
        }
        'release' {
            if ($Arguments[1] -ne 'create') { throw "Unexpected gh command: $Arguments" }
            if ('--notes-file' -in $Arguments) {
                $Call.NotesFile = $Arguments[[array]::IndexOf($Arguments, '--notes-file') + 1]
                $Call.Notes = [System.IO.File]::ReadAllText($Call.NotesFile)
            }
            if ($State.Scenario.UploadFails) { $global:LASTEXITCODE = 1 }
        }
        default { throw "Unexpected gh command: $Arguments" }
    }
}

function Invoke-Scenario {
    param(
        [string]$Name,
        [hashtable]$Options = @{},
        [hashtable]$Parameters = @{},
        [string]$ExpectedError
    )

    $script:Calls.Clear()
    $script:Scenario = @{
        Version = '4.16.0'
        HeadReads = 0
        HeadChanges = $false
        Dirty = $false
        Pushed = $true
        LocalTag = $false
        RemoteTag = $false
        ExistingRelease = $false
        AuthFails = $false
        FetchFails = $false
        ApiFails = $false
        BuildFails = $false
        UploadFails = $false
    }
    foreach ($Key in $Options.Keys) { $script:Scenario[$Key] = $Options[$Key] }
    $global:TextGrabReleaseTestState.Scenario = $script:Scenario

    $Failure = $null
    try {
        & (Join-Path $script:FixtureRoot 'build-exe-for-gh.ps1') @Parameters *> $null
    }
    catch {
        $Failure = $_.Exception.Message
    }
    if ($ExpectedError) {
        Assert-True ($Failure -like "*$ExpectedError*") "$Name should fail with '$ExpectedError', got '$Failure'."
        Assert-True (@($script:Calls | Where-Object { $_.Tool -eq 'gh' -and $_.Arguments[0] -eq 'release' }).Count -eq 0) "$Name must not create a release."
        Assert-True (@($script:Calls | Where-Object { $_.Tool -eq 'git' -and $_.Arguments[0] -eq 'tag' -and $_.Arguments[1] -ne '--list' }).Count -eq 0) "$Name must not create a tag."
    }
    else {
        Assert-True (-not $Failure) "$Name failed: $Failure"
    }
    Write-Output "Passed: $Name"
}

try {
    New-Item -ItemType Directory -Path (Join-Path $FixtureRoot 'Text-Grab') -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $SourceRoot 'build-exe-for-gh.ps1'), (Join-Path $SourceRoot 'build-unpackaged.ps1') -Destination $FixtureRoot
    Set-Content -LiteralPath (Join-Path $FixtureRoot 'Text-Grab\Text-Grab.csproj') -Value '<Project />'

    Invoke-Scenario -Name 'Build-only mode' -Parameters @{ SkipRelease = $true }
    Assert-True (@($Calls | Where-Object Tool -in 'git', 'gh').Count -eq 0) 'Build-only mode must not call git or gh.'
    $Publishes = @($Calls | Where-Object { $_.Tool -eq 'dotnet' -and $_.Arguments[0] -eq 'publish' })
    Assert-True ($Publishes.Count -eq 4) 'Expected four publishes.'
    foreach ($Runtime in 'win-x64', 'win-arm64') {
        foreach ($Mode in '--self-contained', '--no-self-contained') {
            Assert-True (@($Publishes | Where-Object { $Runtime -in $_.Arguments -and $Mode -in $_.Arguments }).Count -eq 1) "Missing $Runtime $Mode publish."
        }
    }

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    foreach ($Architecture in 'x64', 'arm64') {
        $Archive = [System.IO.Compression.ZipFile]::OpenRead((Join-Path $ArtifactRoot "Text-Grab-$Architecture-Self-Contained.zip"))
        try {
            $Executable = if ($Architecture -eq 'x64') { 'Text-Grab.exe' } else { 'Text-Grab-arm64.exe' }
            Assert-True ($null -ne $Archive.GetEntry($Executable)) "$Architecture ZIP must contain $Executable at the root."
            Assert-True ($null -ne $Archive.GetEntry('runtime.dll')) "$Architecture ZIP must include runtime files."
        }
        finally { $Archive.Dispose() }
    }

    Invoke-Scenario -Name 'Draft release with generated notes'
    $Release = @($Calls | Where-Object { $_.Tool -eq 'gh' -and $_.Arguments[0] -eq 'release' })
    Assert-True ($Release.Count -eq 1) 'Expected one release creation.'
    $Arguments = $Release[0].Arguments
    Assert-True ($Arguments[2] -eq 'v4.16.0') 'Tag must use the evaluated app version.'
    foreach ($Flag in '--draft', '--generate-notes') {
        Assert-True ($Flag -in $Arguments) "Missing $Flag."
    }
    foreach ($Pair in @(@('--repo', 'TheJoeFin/Text-Grab'), @('--title', 'v4.16.0'), @('--target', $Commit))) {
        Assert-True ($Arguments[[array]::IndexOf($Arguments, $Pair[0]) + 1] -eq $Pair[1]) "Incorrect $($Pair[0])."
    }
    $ExpectedAssets = @('Text-Grab.exe', 'Text-Grab-arm64.exe', 'Text-Grab-x64-Self-Contained.zip', 'Text-Grab-arm64-Self-Contained.zip')
    $UploadedAssets = @($Arguments | Where-Object { $_ -like "$ArtifactRoot\*" } | ForEach-Object { Split-Path -Leaf $_ })
    Assert-True ($UploadedAssets.Count -eq 4) 'Only four release assets may be uploaded.'
    Assert-True (@(Compare-Object $ExpectedAssets $UploadedAssets).Count -eq 0) 'Incorrect release assets.'
    Assert-True (@($Calls | Where-Object { $_.Tool -eq 'git' -and $_.Arguments[0] -eq 'push' }).Count -eq 0) 'Draft workflow must not push tags.'
    $Tags = @($Calls | Where-Object { $_.Tool -eq 'git' -and $_.Arguments[0] -eq 'tag' -and $_.Arguments[1] -ne '--list' })
    Assert-True ($Tags.Count -eq 1 -and $Tags[0].Arguments[1] -eq 'v4.16.0' -and $Tags[0].Arguments[2] -eq $Commit) 'Local tag must point to the built commit.'

    $Notes = "Keep `"quotes`", apostrophes ' and Unicode: $([char]0x00e9)`nSecond line."
    Invoke-Scenario -Name 'Prerelease with custom notes' -Options @{ Version = '4.16.0-beta.2' } -Parameters @{ Prerelease = $true; Notes = $Notes }
    $Release = @($Calls | Where-Object { $_.Tool -eq 'gh' -and $_.Arguments[0] -eq 'release' })[0]
    Assert-True ($Release.Arguments[2] -eq 'v4.16.0-beta.2') 'Prerelease version suffix must be preserved.'
    Assert-True ('--prerelease' -in $Release.Arguments -and '--generate-notes' -notin $Release.Arguments) 'Incorrect prerelease/custom notes flags.'
    Assert-True ($Release.Notes -ceq $Notes) 'Custom notes must preserve quotes, newlines and Unicode.'
    Assert-True (-not (Test-Path -LiteralPath $Release.NotesFile)) 'Temporary notes file must be removed.'

    Invoke-Scenario -Name 'Dirty tree' -Options @{ Dirty = $true } -ExpectedError 'Working tree has uncommitted changes'
    Invoke-Scenario -Name 'Explicit dirty-tree override' -Options @{ Dirty = $true } -Parameters @{ AllowDirty = $true }
    Invoke-Scenario -Name 'Unpushed commit' -Options @{ Pushed = $false } -ExpectedError 'HEAD has not been pushed'
    Invoke-Scenario -Name 'Existing local tag' -Options @{ LocalTag = $true } -ExpectedError 'already exists'
    Invoke-Scenario -Name 'Existing remote tag' -Options @{ RemoteTag = $true } -ExpectedError 'already exists'
    Invoke-Scenario -Name 'Existing draft release' -Options @{ ExistingRelease = $true } -ExpectedError 'already exists on GitHub'
    Invoke-Scenario -Name 'Authentication failure' -Options @{ AuthFails = $true } -ExpectedError 'not authenticated'
    Invoke-Scenario -Name 'Fetch failure' -Options @{ FetchFails = $true } -ExpectedError 'failed with exit code'
    Invoke-Scenario -Name 'Release lookup failure' -Options @{ ApiFails = $true } -ExpectedError 'Could not check existing GitHub releases'
    Invoke-Scenario -Name 'Publish failure' -Options @{ BuildFails = $true } -ExpectedError 'publish failed'
    Invoke-Scenario -Name 'Changed HEAD during build' -Options @{ HeadChanges = $true } -ExpectedError 'HEAD changed during the build'
    Invoke-Scenario -Name 'Invalid evaluated version' -Options @{ Version = 'unexpected output' } -ExpectedError 'Invalid app version'

    $Calls.Clear()
    $Scenario.UploadFails = $true
    $Scenario.Version = '4.16.0'
    $Scenario.HeadChanges = $false
    $Failure = $null
    try {
        & (Join-Path $FixtureRoot 'build-exe-for-gh.ps1') -Notes $Notes *> $null
    }
    catch { $Failure = $_.Exception.Message }
    Assert-True ($Failure -like '*Release creation/upload failed*') 'Upload failures must report recovery instructions.'
    $Release = @($Calls | Where-Object { $_.Tool -eq 'gh' -and $_.Arguments[0] -eq 'release' })[0]
    Assert-True (-not (Test-Path -LiteralPath $Release.NotesFile)) 'Failed uploads must also remove the notes file.'

    Write-Output 'GitHub release orchestration validation passed. No real git, dotnet or GitHub commands were executed.'
}
finally {
    Remove-Item -LiteralPath $FixtureRoot -Recurse -Force
    Remove-Variable -Name TextGrabReleaseTestState -Scope Global
}
