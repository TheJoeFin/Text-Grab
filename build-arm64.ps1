$BuildPath = "$PSScriptRoot\bld\arm64"
$BuildPathSC = "$PSScriptRoot\bld\arm64\Text-Grab-Self-Contained"
$Version = Get-Date -Format "yyyy-MM-dd" # 2020-11-1
$VersionDot = $Version -replace '-', '.'
$Project = "Text-Grab"
$Archive = "$BuildPath\$Project-$Version.zip"
$ArchiveSC = "$BuildPath\$Project-Self-Contained-$Version.zip"

# Clean up
if (Test-Path -Path $BuildPath) {
	Remove-Item $BuildPath -Recurse
}

# Create the build path
New-Item -ItemType Directory -Path $BuildPath -Force | Out-Null

# Dotnet restore and build
dotnet publish "$PSScriptRoot\$Project\$Project.csproj" `
	--runtime win-arm64 `
	--self-contained false `
	-c Release `
	-v minimal `
	-o $BuildPath `
	-p:PublishSingleFile=true `
    -p:EnableMsixTooling=true `
	-p:CopyOutputSymbolsToPublishDirectory=false `
	-p:Version=$VersionDot `
	--nologo

# Dotnet restore and build
dotnet publish "$PSScriptRoot\$Project\$Project.csproj" `
	--runtime win-arm64 `
	--self-contained true `
	-c Release `
	-v minimal `
	-o $BuildPathSC `
	-p:PublishSingleFile=true `
    -p:EnableMsixTooling=true `
	-p:CopyOutputSymbolsToPublishDirectory=false `
	-p:Version=$VersionDot `
	--nologo

# Archive Build
Compress-Archive -Path "$BuildPathSC" -DestinationPath $ArchiveSC
