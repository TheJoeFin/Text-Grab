$BuildPath = "$PSScriptRoot\bld\x64"
$BuildPathSC = "$PSScriptRoot\bld\x64\Text-Grab-Self-Contained"
$Version = Get-Date -Format "yyyy-MM-dd" # 2020-11-1
$VersionDot = $Version -replace '-','.'
$Project = "Text-Grab"
$Archive = "$BuildPath\$Project-$Version.zip"
$ArchiveSC = "$BuildPath\$Project-SC-$Version.zip"

# Clean up
if(Test-Path -Path $BuildPath)
{
    Remove-Item $BuildPath -Recurse
}

# Dotnet restore and build
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

# Archive Build
Compress-Archive -Path "$BuildPath\$Project.exe" -DestinationPath $Archive

# Dotnet restore and build
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

# Archive Build
Compress-Archive -Path "$BuildPathSC" -DestinationPath $ArchiveSC