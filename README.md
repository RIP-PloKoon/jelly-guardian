# Jellyfin Profanity Filter Plugin

A Jellyfin plugin inspired by the TV Guardian/Parent Guardian hardware device. Automatically detects and mutes profanity in video content by analyzing subtitle files.

## Features

- 🎯 **Subtitle-based detection**: Scans SRT and VTT subtitle files for profanity
- 🔇 **Real-time muting**: Automatically mutes audio during detected profanity
- ⚙️ **Configurable**: Customize word lists, mute padding, and behavior
- 👤 **Per-user toggle**: Each user can enable/disable the filter independently
- 📊 **Scheduled scanning**: Automatically scans library for new content
- 🎬 **Precision muting**: Mute just the word or entire sentence
- 🌐 **Client-side**: Works in the web player without re-encoding video

## How It Works

Similar to the TV Guardian hardware filter:

1. **Scan Phase**: Plugin scans subtitle files in your library
2. **Detection**: Identifies profanity using configurable word list
3. **Metadata Generation**: Creates timing metadata for each detected word
4. **Playback**: Client-side script monitors playback and mutes audio at precise timestamps

## Prerequisites

### Required Plugin: Subtitle Extractor

This plugin **requires text subtitles** to function. You have three options:

1. **Jellyfin Subtitle Extractor Plugin** (Recommended)
   - Extracts embedded PGS/VOBSUB subtitles from MKV/MP4 files
   - Uses OCR to convert image-based subtitles to text
   - Install from Jellyfin Plugin Catalog
   - Run extraction task on your library

2. **External SRT Files**
   - Download subtitles from OpenSubtitles.org
   - Place `.srt` files next to your movie files
   - Example: `Deadpool (2016).srt` next to `Deadpool (2016).mkv`

3. **Whisper AI Transcription**
   - Use OpenAI's Whisper to generate subtitles from audio
   - Run: `whisper "movie.mkv" --model medium --language en`
   - Generates accurate `.srt` files

**Note:** Movies without text subtitles will be skipped during scanning.

## Installation

### From Repository

1. Open Jellyfin Dashboard
2. Go to **Plugins** → **Repositories**
3. Add custom repository (when available)
4. Install **Profanity Filter** from the catalog

### Manual Installation

1. Download the latest release DLL
2. Copy to your Jellyfin plugins directory:
   - Windows: `%ProgramData%\Jellyfin\Server\plugins\ProfanityFilter\`
   - Linux: `/var/lib/jellyfin/plugins/ProfanityFilter/`
3. Restart Jellyfin server
4. Ensure Subtitle Extractor plugin is installed and configured

## Configuration

### Plugin Settings

Navigate to **Dashboard** → **Plugins** → **Profanity Filter**

- **Profanity Word List**: Comma-separated list of words to detect
- **Mute Padding**: Milliseconds to pad before/after detected words (default: 100ms)
- **Mute Entire Sentence**: Toggle between word-only or sentence muting
- **Enabled by Default**: Whether filter is on for new users

### User Settings

Each user can toggle the filter via:
- API endpoint: `/ProfanityFilter/UserPreferences/{userId}`
- Console command: `profanityFilter.toggleFilter()`

## Building from Source

### Prerequisites

- .NET 9.0 SDK
- Jellyfin server (for testing)

### Build Steps

```bash
# Clone the repository
cd /tmp/jellyfin-profanity-filter

# Build the plugin
dotnet build Jellyfin.Plugin.ProfanityFilter.sln --configuration Release

# Output DLL will be in:
# Jellyfin.Plugin.ProfanityFilter/bin/Release/net9.0/Jellyfin.Plugin.ProfanityFilter.dll
```

## Usage

### Initial Scan

After installation, run the scheduled task:

1. Go to **Dashboard** → **Scheduled Tasks**
2. Find **Scan Library for Profanity**
3. Click **Run**

This will scan all video files with subtitles and generate filter metadata.

### Enabling for Users

Users can enable/disable via API or by editing preferences. In the future, a UI toggle will be added to the player controls.

### Testing

To verify the filter is working:

1. Play a video with known profanity
2. Check browser console for `[ProfanityFilter]` logs
3. Audio should mute during detected words

## Architecture

```
┌─────────────────────┐
│  Subtitle Files     │
│  (.srt, .vtt)      │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│  SubtitleParser     │
│  Parse timing/text  │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│  ProfanityDetector  │
│  Pattern matching   │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ MuteTimestampGen    │
│ Generate ranges     │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│  Metadata JSON      │
│  item.profanity.json│
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│  Client Player      │
│  Real-time muting   │
└─────────────────────┘
```

## API Endpoints

### Get Metadata
```
GET /ProfanityFilter/Metadata/{itemId}
```
Returns profanity filter metadata for a specific item.

### Get User Preferences
```
GET /ProfanityFilter/UserPreferences/{userId}
```
Returns user's filter preferences.

### Update User Preferences
```
POST /ProfanityFilter/UserPreferences/{userId}
Body: { "Enabled": true, "MuteEntireSentence": false }
```
Updates user's filter preferences.

## Limitations

- **Requires subtitles**: Only works with content that has external subtitle files
- **Language support**: Currently English-focused (word list can be customized)
- **Embedded subtitles**: Does not extract from video streams (use Subtitle Extract plugin first)
- **Streaming services**: Not compatible with external streaming sources
- **Client support**: Currently web player only (mobile apps need integration)

## Comparison to TV Guardian

| Feature | TV Guardian | This Plugin |
|---------|-------------|-------------|
| Video Type | Broadcast/Component | Streaming |
| Detection Method | Closed captions | Subtitle files |
| Latency | Real-time | ~100ms |
| Customization | Limited | Fully customizable |
| Per-user | No | Yes |
| Cost | $50-200 | Free |

## Roadmap

- [ ] UI toggle in player controls
- [ ] Multi-language support
- [ ] Custom word lists per library
- [ ] Integration with subtitle extract plugin
- [ ] Mobile app support
- [ ] Fuzzy matching for variations
- [ ] User-submitted corrections
- [ ] Statistics dashboard

## Contributing

Contributions welcome! Please:

1. Fork the repository
2. Create a feature branch
3. Follow C# coding conventions
4. Add tests for new features
5. Submit pull request

## License

This project is licensed under the GPL-3.0 License - see LICENSE file for details.

## Acknowledgments

- Inspired by TV Guardian hardware filter
- Built on the Jellyfin plugin framework
- Thanks to the Jellyfin community

## Support

- [GitHub Issues](https://github.com/yourusername/jellyfin-profanity-filter/issues)
- [Jellyfin Forum](https://forum.jellyfin.org/)
- [Jellyfin Matrix](https://matrix.to/#/#jellyfin:matrix.org)

## Disclaimer

This plugin is provided as-is for parental control purposes. Profanity detection is based on pattern matching and may not catch all variations. Always review content appropriately.
