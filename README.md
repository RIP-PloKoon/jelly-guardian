# Jelly Guardian / Jellyfin Profanity Filter

A Jellyfin plugin that scans text subtitles for profanity and mutes matching playback ranges in the Jellyfin web player.

This is the maintained `RIP-PloKoon` fork of the original [`Light1Knight/jelly-guardian`](https://github.com/Light1Knight/jelly-guardian) project. The fork keeps the original TV Guardian-inspired idea while focusing on installability through a Jellyfin plugin repository and practical Jellyfin server use.

## Current Status

Latest published plugin version:

```text
1.0.3.0
```

Jellyfin custom repository URL:

```text
https://raw.githubusercontent.com/RIP-PloKoon/jellyfin-profanity-filter-repo/main/manifest.json
```

Compiled plugin packages are hosted in:

```text
https://github.com/RIP-PloKoon/jellyfin-profanity-filter-repo
```

Source code is maintained in this repository:

```text
https://github.com/RIP-PloKoon/jelly-guardian
```

## Features

- Scans movie and episode subtitles for configured profanity words.
- Supports `.srt` and `.vtt` text subtitles.
- Can use external subtitle files next to media.
- Can discover Subtitle Extract cache files in Jellyfin appdata.
- Generates compact JSON mute metadata under Jellyfin server data.
- Serves a Jellyfin web client script from the plugin API.
- Supports configurable word list, mute padding, sentence muting, and default enabled state.
- Includes a CleanVid-derived embedded word list when no custom list is configured.

## Requirements

- Tested against Jellyfin 10.11.x. Other Jellyfin versions may work but are not verified yet.
- Jellyfin web client for playback muting.
- Text subtitles for the media you want filtered, such as SRT/SubRip or WebVTT.
- Subtitle Extract or external `.srt` / `.vtt` files if your media does not already have usable text subtitles.

The plugin does not transcribe audio or OCR subtitle images by itself. It cannot currently scan image-based subtitles such as DVD subtitles (`DVDSUB`) or Blu-ray PGS subtitles. It needs text subtitles before it can scan.

## Install From Jellyfin Repository

1. Open Jellyfin Dashboard.
2. Go to `Plugins -> Repositories`.
3. Add this repository URL:

```text
https://raw.githubusercontent.com/RIP-PloKoon/jellyfin-profanity-filter-repo/main/manifest.json
```

4. Go to `Plugins -> Catalog`.
5. Install `Profanity Filter`.
6. Restart Jellyfin.
7. Open `Dashboard -> Plugins -> Profanity Filter` and save the desired settings.

See [INSTALL.md](INSTALL.md) for verification and troubleshooting steps.

## Subtitle Setup

The scan can use:

- External subtitles next to media, such as `Movie Name.srt` or `Movie Name.en.srt`.
- Subtitle Extract cache files under Jellyfin `DataPath/subtitles/...`.
- Older Jellyfin subtitle cache layouts checked by item ID.

For best results, install and run Jellyfin's Subtitle Extract plugin before running the profanity scan.

Media that only has image-based subtitle streams, such as `DVDSUB` or `PGS`, will not generate profanity metadata unless a text subtitle track is also available.

## Run The Scan

After installation and subtitle extraction:

1. Go to `Dashboard -> Scheduled Tasks`.
2. Run `Scan Library for Profanity`.
3. Check Jellyfin logs for a summary like:

```text
Profanity filter scan complete. Processed: 3180, SubtitlesFound: 1200, NoSubtitle: 1980, WithMatches: 400, Generated: 400, WriteErrors: 0
```

Generated metadata is stored under Jellyfin's server data path:

```text
{DataPath}/profanity-filter/{itemId:N}.json
```

## Web Client Script

The plugin serves its client script at:

```text
/ProfanityFilter/profanity-filter.js
```

If Jellyfin does not load the script automatically, add this to Jellyfin web custom JavaScript:

```javascript
(function() {
    const script = document.createElement('script');
    script.src = '/ProfanityFilter/profanity-filter.js';
    document.head.appendChild(script);
})();
```

During playback, the browser console should expose:

```javascript
window.profanityFilter
profanityFilter.getStatus()
profanityFilter.toggleFilter()
```

## API

Get generated metadata for an item:

```http
GET /ProfanityFilter/Metadata/{itemId}
```

Get user preferences:

```http
GET /ProfanityFilter/UserPreferences/{userId}
```

Update user preferences:

```http
POST /ProfanityFilter/UserPreferences/{userId}
Content-Type: application/json

{ "Enabled": true, "MuteEntireSentence": false }
```

Get the client script:

```http
GET /ProfanityFilter/profanity-filter.js
```

## Build From Source

```bash
dotnet build Jellyfin.Plugin.ProfanityFilter.sln --configuration Release
```

The plugin DLL is generated under:

```text
Jellyfin.Plugin.ProfanityFilter/bin/Release/net9.0/
```

Manual DLL installation is an advanced fallback. Normal users should install from the Jellyfin custom repository URL above so Jellyfin can manage package versions.

## Repository Layout

```text
Jellyfin.Plugin.ProfanityFilter/        Main Jellyfin plugin project
Jellyfin.Plugin.ProfanityFilter.Tests/  xUnit tests, not currently wired into the solution
tools/legacy/                          inherited helper scripts and console utilities
CLEANVID_INTEGRATION.md                CleanVid word-list integration notes
INSTALL.md                             current user install and verification guide
```

The inherited helper tools are kept under `tools/legacy/` for developer reference. The maintained user workflow is the Jellyfin custom repository install.

## Limitations

- Requires text subtitles. Image-based subtitle streams such as `DVDSUB` and `PGS` are not scanned yet.
- Currently focused on English word lists.
- Playback muting currently targets Jellyfin web. Other Jellyfin clients are not supported yet.
- Detection is pattern-based and can miss context, variants, or subtitle timing edge cases.
- Per-user preference persistence is still basic.

## Attribution

- Original project: [`Light1Knight/jelly-guardian`](https://github.com/Light1Knight/jelly-guardian)
- Maintained fork and Jellyfin catalog packaging: [`RIP-PloKoon/jelly-guardian`](https://github.com/RIP-PloKoon/jelly-guardian)
- Word-list inspiration and data: CleanVid by Seth Grover, as noted in `CLEANVID_INTEGRATION.md` and `Resources/swears.txt`.

## License

This project follows the original repository license. See the repository license file where present.
