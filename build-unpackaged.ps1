param(
    [ValidateSet("Prod", "Beta")]
    [string]$Routine
)

$ErrorActionPreference = "Stop"

$Project = "Text-Grab"
$ProjectPath = "$PSScriptRoot\$Project\$Project.csproj"
$BuildRoot = "$PSScriptRoot\bld"
$PublishRoot = "$BuildRoot\publish"
$ArtifactPath = "$BuildRoot\artifacts"

$BuildPathX64 = "$PublishRoot\x64"
$BuildPathX64SC = "$PublishRoot\x64-self-contained"
$BuildPathArm64 = "$PublishRoot\arm64"
$BuildPathArm64SC = "$PublishRoot\arm64-self-contained"

$ArtifactX64 = "$ArtifactPath\$Project.exe"
$ArtifactArm64 = "$ArtifactPath\$Project-arm64.exe"
$ArtifactX64SC = "$ArtifactPath\$Project-x64-Self-Contained"
$ArtifactArm64SC = "$ArtifactPath\$Project-arm64-Self-Contained"

function Select-BuildRoutine {
    while ($true) {
        Write-Host "Select a build routine:" -ForegroundColor Cyan
        Write-Host "  1. Prod - leave self-contained builds unzipped for signing"
        Write-Host "  2. Beta - create upload-ready ZIP archives"

        switch ((Read-Host "Enter 1 or 2").Trim().ToLowerInvariant()) {
            { $_ -in "1", "prod" } {
                return "Prod"
            }
            { $_ -in "2", "beta" } {
                return "Beta"
            }
            default {
                Write-Host "Please enter 1 for Prod or 2 for Beta.`n" -ForegroundColor Yellow
            }
        }
    }
}

function Invoke-Publish {
    param(
        [Parameter(Mandatory)]
        [string]$Runtime,

        [Parameter(Mandatory)]
        [string]$OutputPath,

        [Parameter(Mandatory)]
        [bool]$SelfContained
    )

    $BuildType = if ($SelfContained) { "self-contained" } else { "framework-dependent" }
    Write-Host "Building $Runtime $BuildType..." -ForegroundColor Yellow

    $Arguments = @(
        "publish"
        $ProjectPath
        "--runtime", $Runtime
        "--configuration", "Release"
        "--verbosity", "minimal"
        "--output", $OutputPath
        "-p:EnableMsixTooling=true"
        "-p:PublishSingleFile=true"
        "-p:CopyOutputSymbolsToPublishDirectory=false"
        "--nologo"
    )

    if ($SelfContained) {
        $Arguments += @(
            "--self-contained"
            "-p:DebugSymbols=false"
            "-p:DebugType=None"
        )
    }
    else {
        $Arguments += "--no-self-contained"
    }

    if ($Runtime -eq "win-x64") {
        $Arguments += "-p:PublishReadyToRun=$($SelfContained.ToString().ToLowerInvariant())"
    }

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "The $Runtime $BuildType publish failed with exit code $LASTEXITCODE."
    }
}

if ([string]::IsNullOrWhiteSpace($Routine)) {
    $Routine = Select-BuildRoutine
}

Write-Host "`nBuilding Text Grab using the $Routine routine..." -ForegroundColor Green

Write-Host "`nCleaning previous build output..." -ForegroundColor Cyan
if (Test-Path $PublishRoot) {
    Remove-Item $PublishRoot -Recurse -Force
}
if (Test-Path $ArtifactPath) {
    Remove-Item $ArtifactPath -Recurse -Force
}

New-Item -ItemType Directory -Path $PublishRoot, $ArtifactPath -Force | Out-Null

Write-Host "`n=== Building x64 Versions ===" -ForegroundColor Magenta
Invoke-Publish -Runtime "win-x64" -OutputPath $BuildPathX64 -SelfContained $false
Invoke-Publish -Runtime "win-x64" -OutputPath $BuildPathX64SC -SelfContained $true

Write-Host "`n=== Building ARM64 Versions ===" -ForegroundColor Magenta
Invoke-Publish -Runtime "win-arm64" -OutputPath $BuildPathArm64 -SelfContained $false
Invoke-Publish -Runtime "win-arm64" -OutputPath $BuildPathArm64SC -SelfContained $true

Write-Host "`n=== Collecting Artifacts ===" -ForegroundColor Magenta
Move-Item "$BuildPathX64\$Project.exe" $ArtifactX64
Move-Item "$BuildPathArm64\$Project.exe" $ArtifactArm64
Rename-Item "$BuildPathArm64SC\$Project.exe" "$Project-arm64.exe"

if ($Routine -eq "Beta") {
    $ArchiveX64SC = "$ArtifactX64SC.zip"
    $ArchiveArm64SC = "$ArtifactArm64SC.zip"

    Write-Host "Creating x64 self-contained archive..." -ForegroundColor Yellow
    Compress-Archive -Path "$BuildPathX64SC\*" -DestinationPath $ArchiveX64SC -Force

    Write-Host "Creating ARM64 self-contained archive..." -ForegroundColor Yellow
    Compress-Archive -Path "$BuildPathArm64SC\*" -DestinationPath $ArchiveArm64SC -Force
}
else {
    Move-Item $BuildPathX64SC $ArtifactX64SC
    Move-Item $BuildPathArm64SC $ArtifactArm64SC
}

Remove-Item $PublishRoot -Recurse -Force

Write-Host "`n=== Version Information ===" -ForegroundColor Cyan
$VersionInfo = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($ArtifactX64)
Write-Host "Product Version: $($VersionInfo.ProductVersion)" -ForegroundColor White
Write-Host "File Version: $($VersionInfo.FileVersion)" -ForegroundColor White

Write-Host "`n=== Artifacts ===" -ForegroundColor Green
Get-ChildItem $ArtifactPath | ForEach-Object {
    Write-Host $_.FullName -ForegroundColor White
}

if ($Routine -eq "Prod") {
    Write-Host "`nSign the executables in the self-contained folders before creating the ZIP archives." -ForegroundColor Yellow
}

Write-Host "`nBuild completed successfully. Artifacts are in: $ArtifactPath" -ForegroundColor Green
