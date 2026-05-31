# Installation Guide

This guide is for installing the maintained `RIP-PloKoon` fork through Jellyfin's custom plugin repository support.

## 1. Add The Plugin Repository

In Jellyfin:

1. Open `Dashboard`.
2. Go to `Plugins -> Repositories`.
3. Add a new repository.
4. Use this repository URL:

```text
https://raw.githubusercontent.com/RIP-PloKoon/jellyfin-profanity-filter-repo/main/manifest.json
```

5. Save.

## 2. Install The Plugin

1. Go to `Dashboard -> Plugins -> Catalog`.
2. Find `Profanity Filter`.
3. Install the latest version.
4. Restart Jellyfin.
5. Confirm the plugin appears under `Dashboard -> Plugins`.

Current published version:

```text
1.0.3.0
```

Compatibility:

```text
Tested against Jellyfin 10.11.x. Other Jellyfin versions may work but are not verified yet.
```

## 3. Prepare Text Subtitles

The plugin requires text subtitles. It cannot filter media until it can read `.srt` or `.vtt` subtitle text.

Image-based subtitle streams are not supported yet. Media that only has DVD subtitles (`DVDSUB`) or Blu-ray PGS subtitles will not generate profanity metadata unless a text subtitle track is also available.

Recommended path:

1. Install Jellyfin's Subtitle Extract plugin.
2. Configure it for the subtitle languages you use.
3. Run the Subtitle Extract scheduled task.
4. Wait for extraction to finish before running the profanity scan.

Alternative path:

Place external `.srt` or `.vtt` files next to your media files:

```text
/Movies/Movie Name (2024)/Movie Name (2024).mkv
/Movies/Movie Name (2024)/Movie Name (2024).srt
```

Language-specific files are also checked:

```text
Movie Name (2024).en.srt
```

## 4. Configure The Plugin

Open `Dashboard -> Plugins -> Profanity Filter`.

Settings:

- `Enabled by default`: whether filtering starts enabled for users.
- `Profanity words`: comma-separated custom words. Leave blank to use the built-in word list.
- `Mute padding milliseconds`: extra time before and after each detected word.
- `Mute entire sentence`: mute the full subtitle line instead of only the estimated word range.
- `Enable word replacement`: generate replacement text when mappings are available.
- `Use grammatical replacement`: choose replacements using lightweight context rules.

Save the settings before scanning.

## 5. Run The Scan

1. Go to `Dashboard -> Scheduled Tasks`.
2. Run `Scan Library for Profanity`.
3. Watch Jellyfin logs for the final scan summary.

Expected log shape:

```text
Profanity filter scan complete. Processed: 3180, SubtitlesFound: 1200, NoSubtitle: 1980, WithMatches: 400, Generated: 400, WriteErrors: 0
```

Healthy signs:

- `SubtitlesFound` is greater than zero.
- `Generated` is greater than zero for a library with matching words.
- `WriteErrors` is zero.

Generated metadata is written under Jellyfin's server data path:

```text
{DataPath}/profanity-filter/{itemId:N}.json
```

For Docker users, `{DataPath}` depends on your container volume mapping. Check your Jellyfin container or appdata path rather than assuming a host path from this guide.

## 6. Load The Web Client Script

The plugin exposes the web client script here:

```text
http://YOUR_JELLYFIN_SERVER:8096/ProfanityFilter/profanity-filter.js
```

If Jellyfin web does not load it automatically, add this to Jellyfin's custom JavaScript field:

```javascript
(function() {
    const script = document.createElement('script');
    script.src = '/ProfanityFilter/profanity-filter.js';
    document.head.appendChild(script);
})();
```

Restart or refresh the browser after saving.

## 7. Verify Playback

Open Jellyfin web, start playback for an item that generated metadata, then check the browser console:

```javascript
window.profanityFilter
profanityFilter.getStatus()
```

Expected status for a matched item:

```javascript
{
  enabled: true,
  muteRangeCount: 1,
  currentItemId: "..."
}
```

`muteRangeCount` should be greater than zero for media with generated metadata.

You can toggle the filter from the console:

```javascript
profanityFilter.toggleFilter()
```

## Troubleshooting

Plugin does not appear:

- Confirm the repository URL was saved correctly.
- Restart Jellyfin after install.
- Check Jellyfin logs for plugin load errors.

No subtitles found:

- Confirm Subtitle Extract has finished.
- Confirm external `.srt` or `.vtt` files are visible inside the Jellyfin container.
- Confirm the media has text subtitles. Image subtitle formats such as `DVDSUB` and `PGS` are not readable by this plugin yet.
- Check Jellyfin logs for the scan summary counters.

No metadata generated:

- Confirm `SubtitlesFound` is greater than zero.
- Confirm the configured word list is not accidentally empty because of custom settings.
- Leave `Profanity words` blank to use the built-in list.
- Check `WriteErrors` in the scan summary.

Script endpoint fails:

- Open `/ProfanityFilter/profanity-filter.js` directly in a browser.
- Restart Jellyfin after plugin updates.
- Confirm the installed plugin version is current.

Playback does not mute:

- Confirm the script loaded in the browser console.
- Confirm `profanityFilter.getStatus()` reports the current item ID.
- Confirm `muteRangeCount` is greater than zero.
- Confirm browser playback is using Jellyfin web. Other Jellyfin clients are not supported yet.

## Advanced Manual Install

Manual DLL installation is not the normal path for this fork. Prefer the custom repository install above.

For development only:

```bash
dotnet build Jellyfin.Plugin.ProfanityFilter.sln --configuration Release
```

Then copy the release output from:

```text
Jellyfin.Plugin.ProfanityFilter/bin/Release/net9.0/
```

to an appropriate Jellyfin plugin directory and restart Jellyfin. Directory names and writable paths vary by platform, package type, and Docker volume mapping.
