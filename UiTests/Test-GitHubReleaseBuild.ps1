[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$Root = Join-Path ([System.IO.Path]::GetTempPath()) ('text-grab-release-build-' + [guid]::NewGuid().ToString('N'))
$ArtifactRoot = Join-Path $Root 'bld\artifacts'
$ScriptPath = Join-Path $Root 'build-exe-for-gh.ps1'
$SigningMode = 'Success'
$SigningCalls = 0

function Assert-True {
    param([bool]$Condition, [string]$Message)

    if (-not $Condition) { throw $Message }
}

function dotnet {
    & $env:ComSpec /c 'exit 0'
    '4.16.0'
}

function sign-exe {
    param([string]$SearchPath)

    $script:SigningCalls++
    Assert-True ($SearchPath -eq $ArtifactRoot) 'Signing must target the complete artifact folder.'
    & $env:ComSpec /c 'exit 0'
    if ($SigningMode -eq 'Cancelled') { return }
    if ($SigningMode -eq 'Failed') {
        & $env:ComSpec /c 'exit 7'
        return
    }
    if ($SigningMode -eq 'Throws') { throw 'Signing service unavailable.' }

    Get-ChildItem -LiteralPath $SearchPath -Filter '*.exe' -File -Recurse | ForEach-Object {
        if ($SigningMode -ne 'Partial' -or $_.Name -ne 'helper.exe') {
            Set-Content -LiteralPath $_.FullName -Value 'signed'
        }
    }
}

function Get-AuthenticodeSignature {
    param([string]$LiteralPath)

    $Status = if ((Get-Content -LiteralPath $LiteralPath -Raw).Trim() -eq 'signed') { 'Valid' } else { 'NotSigned' }
    if ($SigningMode -eq 'Invalid') { $Status = 'HashMismatch' }
    [pscustomobject]@{ Status = $Status }
}

try {
    New-Item -ItemType Directory -Path (Join-Path $Root 'Text-Grab') -Force | Out-Null
    Set-Content -LiteralPath (Join-Path $Root 'Text-Grab\Text-Grab.csproj') -Value '<Project />'
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot '..\build-exe-for-gh.ps1') -Destination $ScriptPath
    Set-Content -LiteralPath (Join-Path $Root 'build-unpackaged.ps1') -Value @'
param([string]$Routine)

if ($Routine -ne 'Prod') { throw 'The release must request unzipped builds before signing.' }
$Artifacts = Join-Path $PSScriptRoot 'bld\artifacts'
if (Test-Path -LiteralPath $Artifacts) {
    Remove-Item -LiteralPath $Artifacts -Recurse -Force
}
New-Item -ItemType Directory -Path $Artifacts -Force | Out-Null
foreach ($Name in 'Text-Grab.exe', 'Text-Grab-arm64.exe') {
    Set-Content -LiteralPath (Join-Path $Artifacts $Name) -Value 'unsigned'
}
foreach ($Architecture in 'x64', 'arm64') {
    $Directory = Join-Path $Artifacts "Text-Grab-$Architecture-Self-Contained"
    New-Item -ItemType Directory -Path (Join-Path $Directory 'tools') -Force | Out-Null
    $ExeName = if ($Architecture -eq 'arm64') { 'Text-Grab-arm64.exe' } else { 'Text-Grab.exe' }
    Set-Content -LiteralPath (Join-Path $Directory $ExeName) -Value 'unsigned'
    Set-Content -LiteralPath (Join-Path $Directory 'tools\helper.exe') -Value 'unsigned'
    Set-Content -LiteralPath (Join-Path $Directory 'dependency.dll') -Value 'dependency'
}
& $env:ComSpec /c 'exit 0'
'@

    & $ScriptPath -SkipRelease
    Assert-True ($SigningCalls -eq 1) 'The release must invoke sign-exe exactly once.'
    Assert-True (@(Get-ChildItem -LiteralPath $ArtifactRoot).Count -eq 4) 'The build must produce exactly four release artifacts.'
    foreach ($Name in 'Text-Grab.exe', 'Text-Grab-arm64.exe') {
        $Content = (Get-Content -LiteralPath (Join-Path $ArtifactRoot $Name) -Raw).Trim()
        Assert-True ($Content -eq 'signed') "Standalone executable was not signed: $Name"
    }
    foreach ($Architecture in 'x64', 'arm64') {
        $Archive = Join-Path $ArtifactRoot "Text-Grab-$Architecture-Self-Contained.zip"
        $ArchiveContent = [System.IO.Compression.ZipFile]::OpenRead($Archive)
        try {
            $Executables = @($ArchiveContent.Entries | Where-Object { $_.Name -like '*.exe' })
            Assert-True ($Executables.Count -eq 2) "The $Architecture archive must retain both executables."
            foreach ($Entry in $Executables) {
                $Reader = [System.IO.StreamReader]::new($Entry.Open())
                try {
                    Assert-True ($Reader.ReadToEnd().Trim() -eq 'signed') "An executable was zipped before signing: $($Entry.FullName)"
                }
                finally {
                    $Reader.Dispose()
                }
            }
            Assert-True (@($ArchiveContent.Entries | Where-Object { $_.Name -eq 'dependency.dll' }).Count -eq 1) 'ZIP creation must retain dependencies.'
        }
        finally {
            $ArchiveContent.Dispose()
        }
    }

    foreach ($SigningMode in 'Cancelled', 'Failed', 'Throws', 'Partial', 'Invalid') {
        $ExpectedError = switch ($SigningMode) {
            'Failed' { '*sign-exe failed with exit code 7*' }
            'Throws' { '*Signing service unavailable*' }
            default { '*does not have a valid signature*' }
        }
        $Failure = $null
        try {
            & $ScriptPath -SkipRelease
        }
        catch {
            $Failure = $_.Exception.Message
        }
        Assert-True ($Failure -like $ExpectedError) "The release must stop on $SigningMode signing. Got: $Failure"
        Assert-True (@(Get-ChildItem -LiteralPath $ArtifactRoot -Filter '*.zip').Count -eq 0) "ZIPs must not be created after $SigningMode signing."
    }

    Remove-Item Function:\sign-exe
    Remove-Item -LiteralPath $ArtifactRoot -Recurse -Force
    $Failure = $null
    try {
        & $ScriptPath -SkipRelease
    }
    catch {
        $Failure = $_.Exception.Message
    }
    Assert-True ($Failure -like '*sign-exe is required*') "A missing signing profile must produce an actionable error. Got: $Failure"
    Assert-True (-not (Test-Path -LiteralPath $ArtifactRoot)) 'Missing signing tooling must stop the release before building.'

    Write-Output 'GitHub release signing workflow validation passed.'
}
finally {
    if (Test-Path -LiteralPath $Root) {
        Remove-Item -LiteralPath $Root -Recurse -Force
    }
}
