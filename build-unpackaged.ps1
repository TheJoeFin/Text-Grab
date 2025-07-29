$Version = Get-Date -Format "yyyy-MM-dd"
$Project = "Text-Grab"

# Define build paths for both architectures
$BuildPathX64 = "$PSScriptRoot\bld\x64"
$BuildPathX64SC = "$PSScriptRoot\bld\x64\Text-Grab-Self-Contained"
$BuildPathARM64 = "$PSScriptRoot\bld\arm64"
$BuildPathARM64SC = "$PSScriptRoot\bld\arm64\Text-Grab-Self-Contained"

# Define archive paths
$ArchiveX64SC = "$BuildPathX64\$Project-x64-Self-Contained-$Version.zip"
$ArchiveARM64SC = "$BuildPathARM64\$Project-Self-Contained-$Version.zip"

Write-Host "Building Text-Grab for x64 and ARM64 architectures..." -ForegroundColor Green
Write-Host "Build Date: $Version" -ForegroundColor Yellow

# Clean up existing build directories
Write-Host "`nCleaning up existing build directories..." -ForegroundColor Cyan
if (Test-Path -Path $BuildPathX64) {
    Remove-Item $BuildPathX64 -Recurse -Force
}
if (Test-Path -Path $BuildPathARM64) {
    Remove-Item $BuildPathARM64 -Recurse -Force
}

# Create build directories
New-Item -ItemType Directory -Path $BuildPathX64 -Force | Out-Null
New-Item -ItemType Directory -Path $BuildPathARM64 -Force | Out-Null

Write-Host "`n=== Building x64 Versions ===" -ForegroundColor Magenta

# Build x64 Framework-Dependent
Write-Host "Building x64 framework-dependent..." -ForegroundColor Yellow
dotnet publish "$PSScriptRoot\$Project\$Project.csproj" `
    --runtime win-x64 `
    --self-contained false `
    -c Release `
    -v minimal `
    -o $BuildPathX64 `
    -p:EnableMsixTooling=true `
    -p:PublishReadyToRun=true `
    -p:PublishSingleFile=true `
    -p:CopyOutputSymbolsToPublishDirectory=false `
    --nologo

# Build x64 Self-Contained
Write-Host "Building x64 self-contained..." -ForegroundColor Yellow
dotnet publish "$PSScriptRoot\$Project\$Project.csproj" `
    --runtime win-x64 `
    --self-contained true `
    -c Release `
    -v minimal `
    -o $BuildPathX64SC `
    -p:EnableMsixTooling=true `
    -p:PublishReadyToRun=true `
    -p:PublishSingleFile=true `
    -p:CopyOutputSymbolsToPublishDirectory=false `
    --nologo

Write-Host "`n=== Building ARM64 Versions ===" -ForegroundColor Magenta

# Build ARM64 Framework-Dependent
Write-Host "Building ARM64 framework-dependent..." -ForegroundColor Yellow
dotnet publish "$PSScriptRoot\$Project\$Project.csproj" `
    --runtime win-arm64 `
    --self-contained false `
    -c Release `
    -v minimal `
    -o $BuildPathARM64 `
    -p:PublishSingleFile=true `
    -p:EnableMsixTooling=true `
    -p:CopyOutputSymbolsToPublishDirectory=false `
    --nologo

# Build ARM64 Self-Contained
Write-Host "Building ARM64 self-contained..." -ForegroundColor Yellow
dotnet publish "$PSScriptRoot\$Project\$Project.csproj" `
    --runtime win-arm64 `
    --self-contained true `
    -c Release `
    -v minimal `
    -o $BuildPathARM64SC `
    -p:PublishSingleFile=true `
    -p:EnableMsixTooling=true `
    -p:CopyOutputSymbolsToPublishDirectory=false `
    --nologo

Write-Host "`n=== Renaming ARM64 Executables ===" -ForegroundColor Magenta

# Rename ARM64 Framework-Dependent executable
Write-Host "Renaming ARM64 framework-dependent executable..." -ForegroundColor Yellow
if (Test-Path "$BuildPathARM64\$Project.exe") {
    Rename-Item "$BuildPathARM64\$Project.exe" "Text-Grab-arm64.exe"
}

# Rename ARM64 Self-Contained executable
Write-Host "Renaming ARM64 self-contained executable..." -ForegroundColor Yellow
if (Test-Path "$BuildPathARM64SC\$Project.exe") {
    Rename-Item "$BuildPathARM64SC\$Project.exe" "Text-Grab-arm64.exe"
}

Write-Host "`n=== Creating Archives ===" -ForegroundColor Magenta

# Create x64 Self-Contained Archive
Write-Host "Creating x64 self-contained archive..." -ForegroundColor Yellow
Compress-Archive -Path "$BuildPathX64SC" -DestinationPath $ArchiveX64SC -Force

# Create ARM64 Self-Contained Archive
Write-Host "Creating ARM64 self-contained archive..." -ForegroundColor Yellow
Compress-Archive -Path "$BuildPathARM64SC" -DestinationPath $ArchiveARM64SC -Force

Write-Host "`n=== Build Summary ===" -ForegroundColor Green
Write-Host "x64 Framework-Dependent: $BuildPathX64\$Project.exe" -ForegroundColor White
Write-Host "x64 Self-Contained: $BuildPathX64SC\$Project.exe" -ForegroundColor White
Write-Host "x64 Self-Contained Archive: $ArchiveX64SC" -ForegroundColor White
Write-Host "ARM64 Framework-Dependent: $BuildPathARM64\Text-Grab-arm64.exe" -ForegroundColor White
Write-Host "ARM64 Self-Contained: $BuildPathARM64SC\Text-Grab-arm64.exe" -ForegroundColor White
Write-Host "ARM64 Self-Contained Archive: $ArchiveARM64SC" -ForegroundColor White

# Get and display the actual product version from the built executable
Write-Host "`n=== Version Information ===" -ForegroundColor Cyan
$versionInfo = [System.Diagnostics.FileVersionInfo]::GetVersionInfo("$BuildPathX64\$Project.exe")
Write-Host "Product Version: $($versionInfo.ProductVersion)" -ForegroundColor White
Write-Host "File Version: $($versionInfo.FileVersion)" -ForegroundColor White

Write-Host "`nBuild completed successfully!" -ForegroundColor Green
