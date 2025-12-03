# Installation Guide - Jellyfin Profanity Filter

## ⚠️ IMPORTANT: Prerequisites First!

**This plugin REQUIRES subtitles to work.** You must have one of the following:

### Option A: Install Subtitle Extractor Plugin (Recommended)
1. Open Jellyfin Dashboard
2. Go to **Plugins** → **Catalog**
3. Install **"Subtitle Extractor"**
4. Configure it:
   - Enable OCR extraction
   - Set language to English (or your preferred language)
5. Go to **Dashboard** → **Scheduled Tasks**
6. Run **"Extract Subtitle Images"** task
7. Wait for it to complete (can take hours for large libraries)

### Option B: Add SRT Files Manually
Place `.srt` subtitle files next to your movies:
```
/Movies/
  ├── Deadpool (2016).mkv
  ├── Deadpool (2016).srt    ← Add this file
```

---

## Installation Steps

### Step 1: Build the Plugin

```bash
# Navigate to plugin directory
cd /tmp/jellyfin-profanity-filter

# Build the plugin (requires .NET 9.0 SDK)
~/.dotnet/dotnet build --configuration Release
```

**Don't have .NET 9.0?** Install it:
```bash
wget https://dot.net/v1/dotnet-install.sh
chmod +x dotnet-install.sh
./dotnet-install.sh --channel 9.0 --install-dir ~/.dotnet
export PATH="$HOME/.dotnet:$PATH"
```

### Step 2: Find Your Jellyfin Plugins Directory

**Linux:**
```bash
# Default location
/var/lib/jellyfin/plugins/

# Or check your config
grep -r "PluginsPath" /etc/jellyfin/
```

**Your system:**
```bash
/media/michael-erekson/RAID1/Jellyfin/Server/plugins/
```

**Windows:**
```
C:\ProgramData\Jellyfin\Server\plugins\
```

### Step 3: Install the Plugin

```bash
# Set your plugins directory
PLUGINS_DIR="/media/michael-erekson/RAID1/Jellyfin/Server/plugins"

# Create plugin directory (include version number)
mkdir -p "$PLUGINS_DIR/ProfanityFilter_1.0.0"

# Copy the DLL
cp /tmp/jellyfin-profanity-filter/Jellyfin.Plugin.ProfanityFilter/bin/Release/net9.0/Jellyfin.Plugin.ProfanityFilter.dll \
   "$PLUGINS_DIR/ProfanityFilter_1.0.0/"

# Copy dependencies (if they exist)
cp /tmp/jellyfin-profanity-filter/Jellyfin.Plugin.ProfanityFilter/bin/Release/net9.0/*.deps.json \
   "$PLUGINS_DIR/ProfanityFilter_1.0.0/" 2>/dev/null || true

# Verify files
ls -lh "$PLUGINS_DIR/ProfanityFilter_1.0.0/"
```

Expected output:
```
-rw-r--r-- 1 user user  45K Dec  2 12:00 Jellyfin.Plugin.ProfanityFilter.dll
-rw-r--r-- 1 user user 2.1K Dec  2 12:00 Jellyfin.Plugin.ProfanityFilter.deps.json
```

### Step 4: Restart Jellyfin

```bash
# Using systemd
sudo systemctl restart jellyfin

# Or find the process and restart manually
ps aux | grep jellyfin
```

### Step 5: Verify Plugin Loaded

1. Open Jellyfin web interface: `http://your-server:8096`
2. Go to **Dashboard** → **Plugins**
3. Look for **"Profanity Filter"** in the list

**Check logs if plugin doesn't appear:**
```bash
tail -f /var/log/jellyfin/jellyfin.log | grep -i profanity
# Or
journalctl -u jellyfin -f | grep -i profanity
```

---

## First Time Setup

### Configure the Plugin

1. Go to **Dashboard** → **Plugins** → **Profanity Filter**
2. Settings:
   - **Profanity Words**: Pre-loaded list (customize if needed)
   - **Mute Padding**: `100` ms (default, works well)
   - **Mute Entire Sentence**: `false` (mute just the word)
   - **Enabled by Default**: `true` (turn on for all users)
3. Click **Save**

### Run Initial Library Scan

1. Go to **Dashboard** → **Scheduled Tasks**
2. Find **"Scan Library for Profanity"**
3. Click **Run Now**
4. Watch the progress

**Monitor scan progress:**
```bash
tail -f /var/log/jellyfin/jellyfin.log | grep "Profanity"
```

You should see:
```
[INF] Found 1234 video items to scan
[INF] Processing subtitles for: Deadpool
[INF] Generated profanity filter for Deadpool: 50 mute ranges
```

### Install Client-Side JavaScript (Critical!)

The plugin needs JavaScript in the web player to actually mute audio.

**Method 1: Custom CSS/JS (Easiest)**
1. Go to **Dashboard** → **General**
2. Scroll to **Custom CSS** section
3. In the **Custom Javascript** field, paste:
```javascript
// Load profanity filter
(function() {
    const script = document.createElement('script');
    script.src = '/ProfanityFilter/profanity-filter.js';
    document.head.appendChild(script);
})();
```
4. Save and refresh browser

**Method 2: Direct File Copy (More Permanent)**
```bash
# Find Jellyfin web root
WEB_ROOT="/usr/share/jellyfin/web"
# Or on your system: /media/michael-erekson/RAID1/Jellyfin/Server/jellyfin-web

# Copy JavaScript file
sudo cp /tmp/jellyfin-profanity-filter/Jellyfin.Plugin.ProfanityFilter/profanity-filter.js \
    "$WEB_ROOT/modules/profanity-filter.js"

# Verify
ls -lh "$WEB_ROOT/modules/profanity-filter.js"
```

Then add to Custom Javascript:
```javascript
import('/modules/profanity-filter.js');
```

---

## Testing

### Test on a Single Movie

```bash
# Test detection on subtitle file
/tmp/enhanced-tester.sh /path/to/subtitle.srt
```

### Test in Browser

1. Open a movie in Jellyfin web player
2. Open browser console (F12)
3. Type: `profanityFilter.toggleFilter()`
4. Should see: `Profanity filter enabled/disabled`
5. Play movie and check if profanity is muted

### Verify Metadata Files

```bash
# Check for generated metadata
find /media/michael-erekson/RAID1/Jellyfin/Movies/ -name "*.profanity.json"

# View metadata for a specific movie
cat "/path/to/Movie (2016).profanity.json"
```

Expected format:
```json
{
  "version": "1.0",
  "muteRanges": [
    { "start": 65265, "end": 67833, "word": "ass" },
    { "start": 77945, "end": 80812, "word": "shit" }
  ]
}
```

---

## Troubleshooting

### Plugin Not Appearing

**Check plugin files exist:**
```bash
ls -lh /media/michael-erekson/RAID1/Jellyfin/Server/plugins/ProfanityFilter_1.0.0/
```

**Check logs for errors:**
```bash
grep -i "profanity\|error\|exception" /var/log/jellyfin/jellyfin.log | tail -20
```

**Common issue:** Wrong .NET version
```bash
# Plugin needs .NET 9.0
dotnet --version
# Should show: 9.0.x
```

### No Subtitles Found

**Verify Subtitle Extractor ran:**
```bash
find /media/michael-erekson/RAID1/Jellyfin/Server/data/subtitles/ -name "*.srt" | wc -l
```

**Check individual movie:**
```bash
# Find subtitle for specific movie
find /media/michael-erekson/RAID1/Jellyfin/Server/data/subtitles/ -name "*.srt" \
  -exec grep -l "movie dialog" {} \;
```

### Profanity Not Muted During Playback

**1. Check JavaScript loaded:**
- Open browser console (F12)
- Type: `profanityFilter`
- Should see: `Object { toggleFilter: function, ... }`

**2. Check metadata exists:**
- Play a movie
- In console, check: `fetch('/ProfanityFilter/Metadata/ITEM_ID').then(r => r.json()).then(console.log)`
- Should return JSON with mute ranges

**3. Check filter is enabled:**
- Console: `profanityFilter.getStatus()`
- Should show: `enabled: true`

### Profanity Not Detected

**Test the subtitle file manually:**
```bash
# Check if subtitle has profanity
grep -i "fuck\|shit\|damn" /path/to/subtitle.srt

# Test with detection script
/tmp/enhanced-tester.sh /path/to/subtitle.srt
```

**Check word list configuration:**
- Dashboard → Plugins → Profanity Filter
- Verify word list includes the words you expect

---

## User Controls

### Per-User Toggle

Each user can control the filter independently:

**Via API:**
```bash
# Enable for user
curl -X POST http://localhost:8096/ProfanityFilter/UserPreferences/USER_ID \
  -H "Content-Type: application/json" \
  -d '{"enabled": true}'

# Check status
curl http://localhost:8096/ProfanityFilter/UserPreferences/USER_ID
```

**Via Browser Console:**
```javascript
// Toggle on/off
profanityFilter.toggleFilter()

// Check status
profanityFilter.getStatus()
```

### Quick Toggle During Playback

While watching a movie:
1. Press F12 to open console
2. Type: `profanityFilter.toggleFilter()`
3. Filter toggles immediately (no restart needed)

---

## Performance Notes

**Scan Performance:**
- ~1 second per movie with subtitles
- 1000 movie library = ~15 minutes
- Scheduled weekly by default

**Storage Impact:**
- ~5KB metadata per movie
- 1000 movies = ~5MB total

**Playback Performance:**
- Client checks every 100ms
- Negligible CPU impact
- No buffering or lag

---

## Uninstalling

```bash
# Remove plugin directory
rm -rf /media/michael-erekson/RAID1/Jellyfin/Server/plugins/ProfanityFilter_1.0.0

# Remove metadata files (optional)
find /media/michael-erekson/RAID1/Jellyfin/Movies/ -name "*.profanity.json" -delete

# Restart Jellyfin
sudo systemctl restart jellyfin
```

---

## Getting Help

**Check logs first:**
```bash
tail -100 /var/log/jellyfin/jellyfin.log | grep -i profanity
```

**Test components individually:**
1. Subtitle extraction: Check `/data/subtitles/` has `.srt` files
2. Plugin loaded: Check Dashboard → Plugins
3. Scan ran: Check for `.profanity.json` files
4. Client script: Check browser console for errors

**Still having issues?**
- Post logs to GitHub issues
- Include: Jellyfin version, OS, plugin version
- Describe: What you expected vs what happened

---

## Success! 🎉

You should now have:
- ✅ Plugin installed and loaded
- ✅ Library scanned for profanity
- ✅ Metadata files generated
- ✅ Client-side muting working
- ✅ Per-user control available

**Test it:** Play Deadpool and listen for muted profanity!

Your family can now enjoy cleaner content on your self-hosted Jellyfin server. 🎬🔇
