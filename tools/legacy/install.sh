#!/bin/bash
# Quick install script for Jellyfin Profanity Filter Plugin

set -e

echo "╔═══════════════════════════════════════════════════════════╗"
echo "║   Jellyfin Profanity Filter - Quick Install Script       ║"
echo "║   TV Guardian-inspired profanity filtering               ║"
echo "╚═══════════════════════════════════════════════════════════╝"
echo ""

# Check if running as root
if [ "$EUID" -eq 0 ]; then 
   echo "⚠️  Don't run as root. Run as your jellyfin user."
   exit 1
fi

# Detect Jellyfin installation
echo "🔍 Detecting Jellyfin installation..."

JELLYFIN_PLUGINS=""
if [ -d "/var/lib/jellyfin/plugins" ]; then
    JELLYFIN_PLUGINS="/var/lib/jellyfin/plugins"
elif [ -d "/media/$USER/RAID1/Jellyfin/Server/plugins" ]; then
    JELLYFIN_PLUGINS="/media/$USER/RAID1/Jellyfin/Server/plugins"
fi

if [ -z "$JELLYFIN_PLUGINS" ]; then
    echo "❌ Could not find Jellyfin plugins directory"
    echo "Please enter the path manually:"
    read -p "Plugins directory: " JELLYFIN_PLUGINS
fi

if [ ! -d "$JELLYFIN_PLUGINS" ]; then
    echo "❌ Directory does not exist: $JELLYFIN_PLUGINS"
    exit 1
fi

echo "✅ Found Jellyfin plugins: $JELLYFIN_PLUGINS"
echo ""

# Check .NET SDK
echo "🔍 Checking .NET SDK..."
if ! command -v dotnet &> /dev/null; then
    if [ -f "$HOME/.dotnet/dotnet" ]; then
        export PATH="$HOME/.dotnet:$PATH"
        DOTNET="$HOME/.dotnet/dotnet"
    else
        echo "❌ .NET SDK not found"
        echo "Install with: wget https://dot.net/v1/dotnet-install.sh && bash dotnet-install.sh --channel 9.0"
        exit 1
    fi
else
    DOTNET="dotnet"
fi

DOTNET_VERSION=$($DOTNET --version)
echo "✅ .NET version: $DOTNET_VERSION"
echo ""

# Build plugin
echo "🔨 Building plugin..."
cd "$(dirname "$0")"

if [ ! -f "Jellyfin.Plugin.ProfanityFilter.sln" ]; then
    echo "❌ Not in plugin directory. Please run from /tmp/jellyfin-profanity-filter"
    exit 1
fi

$DOTNET build --configuration Release

if [ $? -ne 0 ]; then
    echo "❌ Build failed"
    exit 1
fi

echo "✅ Build successful"
echo ""

# Install plugin
echo "📦 Installing plugin..."
PLUGIN_DIR="$JELLYFIN_PLUGINS/ProfanityFilter_1.0.0"

mkdir -p "$PLUGIN_DIR"

cp Jellyfin.Plugin.ProfanityFilter/bin/Release/net9.0/Jellyfin.Plugin.ProfanityFilter.dll "$PLUGIN_DIR/" || exit 1
cp Jellyfin.Plugin.ProfanityFilter/bin/Release/net9.0/*.deps.json "$PLUGIN_DIR/" 2>/dev/null || true

echo "✅ Plugin installed to: $PLUGIN_DIR"
ls -lh "$PLUGIN_DIR/"
echo ""

# Check for Subtitle Extractor
echo "🔍 Checking prerequisites..."
echo ""
echo "⚠️  IMPORTANT: This plugin requires subtitles!"
echo ""
echo "You need EITHER:"
echo "  1. Subtitle Extractor plugin (install from Jellyfin catalog)"
echo "  2. External .srt files next to your movies"
echo ""
read -p "Have you installed Subtitle Extractor? (y/n): " has_subtitles

if [ "$has_subtitles" != "y" ]; then
    echo ""
    echo "⚠️  Install Subtitle Extractor first:"
    echo "   1. Jellyfin Dashboard → Plugins → Catalog"
    echo "   2. Install 'Subtitle Extractor'"
    echo "   3. Run extraction task on your library"
    echo "   4. Come back and run this script again"
    echo ""
fi

echo ""
echo "🔄 Next step: Restart Jellyfin"
echo ""
echo "Choose restart method:"
echo "  1) sudo systemctl restart jellyfin (systemd)"
echo "  2) Manual restart (you'll do it yourself)"
echo "  3) Skip for now"
read -p "Choice (1/2/3): " restart_choice

case $restart_choice in
    1)
        sudo systemctl restart jellyfin
        echo "✅ Jellyfin restarted"
        ;;
    2)
        echo "⚠️  Please restart Jellyfin manually"
        ;;
    3)
        echo "⚠️  Remember to restart Jellyfin!"
        ;;
esac

echo ""
echo "╔═══════════════════════════════════════════════════════════╗"
echo "║                  Installation Complete!                   ║"
echo "╚═══════════════════════════════════════════════════════════╝"
echo ""
echo "Next steps:"
echo ""
echo "1. ✅ Plugin installed"
echo "2. 🔄 Restart Jellyfin (if not done)"
echo "3. ⚙️  Configure plugin:"
echo "      Dashboard → Plugins → Profanity Filter"
echo "4. 📊 Run scan task:"
echo "      Dashboard → Scheduled Tasks → Scan Library for Profanity"
echo "5. 🎬 Install client script (see INSTALL.md for details)"
echo "6. 🎉 Test by playing a movie!"
echo ""
echo "📖 Full instructions: cat INSTALL.md"
echo "🐛 Troubleshooting: tail -f /var/log/jellyfin/jellyfin.log | grep Profanity"
echo ""
echo "Enjoy family-friendly viewing! 🎬🔇"
