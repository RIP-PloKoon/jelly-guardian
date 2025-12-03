#!/bin/bash
# Quick test script for MovieTester

echo "=== Movie Tester - Quick Test ==="
echo ""

# Check if subtitle file is provided
if [ -z "$1" ]; then
    echo "Usage: ./test-movie.sh <path-to-subtitle-file.srt>"
    echo ""
    echo "Example:"
    echo "  ./test-movie.sh /home/user/Movies/MyMovie/MyMovie.srt"
    echo "  ./test-movie.sh movie.srt"
    echo ""
    exit 1
fi

SUBTITLE_FILE="$1"

if [ ! -f "$SUBTITLE_FILE" ]; then
    echo "Error: File not found: $SUBTITLE_FILE"
    exit 1
fi

echo "Building MovieTester..."
cd "$(dirname "$0")/MovieTester"

if command -v dotnet &> /dev/null; then
    dotnet build --configuration Release
    
    if [ $? -eq 0 ]; then
        echo ""
        echo "Running profanity detection on: $SUBTITLE_FILE"
        echo ""
        dotnet run --no-build --configuration Release -- "$SUBTITLE_FILE"
    else
        echo "Build failed!"
        exit 1
    fi
else
    echo "Error: .NET SDK not installed"
    echo "Install with: sudo snap install dotnet-sdk --classic"
    exit 1
fi
