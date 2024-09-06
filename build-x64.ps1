# Set up paths
$BuildPath = "$PSScriptRoot\bld\x64"
$BuildPathSC = "$PSScriptRoot\bld\x64\Text-Grab-Self-Contained"
$Version = Get-Date -Format "yyyy-MM-dd"
$VersionDot = $Version -replace '-', '.'
$Project = "Text-Grab"
$Archive = "$BuildPath\$Project-$Version.zip"
$ArchiveSC = "$BuildPath\$Project-Self-Contained-$Version.zip"

# Clean up old build directory if it exists
if (Test-Path -Path $BuildPath) {
    Remove-Item $BuildPath -Recurse
    Write-Host "Old build directory removed."
}

# Build non-self-contained version of Text-Grab
Write-Host "Building non-self-contained version of Text-Grab..."
dotnet publish "$PSScriptRoot\$Project\$Project.csproj" `
    --runtime win-x64 `
    --self-contained false `
    -c Release `
    -v minimal `
    -o $BuildPath `
    -p:PublishReadyToRun=true `
    -p:PublishSingleFile=true `
    -p:CopyOutputSymbolsToPublishDirectory=false `
    -p:Version=$VersionDot `
    --nologo

# Archive the non-self-contained build
Compress-Archive -Path "$BuildPath\$Project.exe" -DestinationPath $Archive
Write-Host "Non-self-contained build archived at $Archive."

# Build self-contained version of Text-Grab
Write-Host "Building self-contained version of Text-Grab..."
dotnet publish "$PSScriptRoot\$Project\$Project.csproj" `
    --runtime win-x64 `
    --self-contained true `
    -c Release `
    -v minimal `
    -o $BuildPathSC `
    -p:PublishReadyToRun=true `
    -p:PublishSingleFile=true `
    -p:CopyOutputSymbolsToPublishDirectory=false `
    -p:Version=$VersionDot `
    --nologo

# Archive the self-contained build
Compress-Archive -Path "$BuildPathSC" -DestinationPath $ArchiveSC
Write-Host "Self-contained build archived at $ArchiveSC."

# Define the installation directory for Text-Grab
# Adjust this path to where Text-Grab is built and stored
$textGrabPath = "$BuildPath\TextGrab.exe"

# Check and add Text-Grab to the system PATH if not already present
$currentPath = [System.Environment]::GetEnvironmentVariable("Path", [System.EnvironmentVariableTarget]::Machine)

if ($currentPath -notlike "*$textGrabPath*") {
    # Add Text-Grab to the system PATH
    $newPath = "$currentPath;$textGrabPath"
    [System.Environment]::SetEnvironmentVariable("Path", $newPath, [System.EnvironmentVariableTarget]::Machine)
    Write-Host "Text-Grab has been added to the system PATH."
} else {
    Write-Host "Text-Grab is already in the system PATH."
}

Write-Host "Build complete!"
