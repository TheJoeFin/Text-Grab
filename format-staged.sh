#!/bin/bash
# Format only the C# files that have been modified (staged for commit)
# This helps avoid formatting the entire codebase when there are existing issues

# Get the list of staged C# files
STAGED_FILES=$(git diff --cached --name-only --diff-filter=ACM | grep '\.cs$')

if [ -z "$STAGED_FILES" ]; then
    echo "No staged C# files to format"
    exit 0
fi

echo "Formatting staged C# files:"
echo "$STAGED_FILES"

# Format each file individually
for FILE in $STAGED_FILES; do
    if [ -f "$FILE" ]; then
        echo "Formatting: $FILE"
        dotnet format Text-Grab.sln --include "$FILE" --verbosity quiet
        # Re-stage the file after formatting
        git add "$FILE"
    fi
done

echo "Formatting complete!"
