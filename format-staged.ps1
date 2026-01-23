# Format only the C# files that have been modified (staged for commit)
# This helps avoid formatting the entire codebase when there are existing issues

Write-Host "Checking for staged C# files..." -ForegroundColor Cyan

# Get the list of staged C# files
$stagedFiles = git diff --cached --name-only --diff-filter=ACM | Where-Object { $_ -match '\.cs$' }

if ($null -eq $stagedFiles -or $stagedFiles.Count -eq 0) {
    Write-Host "No staged C# files to format" -ForegroundColor Yellow
    exit 0
}

Write-Host "Formatting staged C# files:" -ForegroundColor Green
$stagedFiles | ForEach-Object { Write-Host "  - $_" }

# Format each file individually
foreach ($file in $stagedFiles) {
    if (Test-Path $file) {
        Write-Host "Formatting: $file" -ForegroundColor Cyan
        dotnet format Text-Grab.sln --include "$file" --verbosity quiet
        # Re-stage the file after formatting
        git add "$file"
    }
}

Write-Host "Formatting complete!" -ForegroundColor Green
