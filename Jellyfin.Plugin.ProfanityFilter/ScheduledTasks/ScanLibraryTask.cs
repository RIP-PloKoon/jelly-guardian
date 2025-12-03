using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Subtitles;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ProfanityFilter.ScheduledTasks;

/// <summary>
/// Scheduled task to scan library and generate profanity filter metadata.
/// </summary>
public class ScanLibraryTask : IScheduledTask
{
    private readonly ILibraryManager _libraryManager;
    private readonly ILogger<ScanLibraryTask> _logger;
    private readonly IApplicationPaths _appPaths;

    /// <summary>
    /// Initializes a new instance of the <see cref="ScanLibraryTask"/> class.
    /// </summary>
    /// <param name="libraryManager">Instance of the <see cref="ILibraryManager"/> interface.</param>
    /// <param name="logger">Instance of the <see cref="ILogger"/> interface.</param>
    /// <param name="appPaths">Instance of the <see cref="IApplicationPaths"/> interface.</param>
    public ScanLibraryTask(
        ILibraryManager libraryManager,
        ILogger<ScanLibraryTask> logger,
        IApplicationPaths appPaths)
    {
        _libraryManager = libraryManager;
        _logger = logger;
        _appPaths = appPaths;
    }

    /// <inheritdoc />
    public string Name => "Scan Library for Profanity";

    /// <inheritdoc />
    public string Key => "ProfanityFilterScan";

    /// <inheritdoc />
    public string Description => "Scans media library subtitles and generates profanity filter metadata";

    /// <inheritdoc />
    public string Category => "Profanity Filter";

    /// <inheritdoc />
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting profanity filter library scan");

        var config = Plugin.Instance?.Configuration;
        if (config == null)
        {
            _logger.LogError("Plugin configuration not available");
            return;
        }

        var parser = new Services.SubtitleParser();
        var detector = new Services.ProfanityDetector(config.ProfanityWords, config.EnableWordReplacement, config.UseGrammaticalReplacement);
        var generator = new Services.MuteTimestampGenerator();

        // Get all video items from library
        var query = new InternalItemsQuery
        {
            IncludeItemTypes = new[] { BaseItemKind.Movie, BaseItemKind.Episode },
            IsVirtualItem = false,
            Recursive = true
        };

        var items = _libraryManager.GetItemList(query);
        _logger.LogInformation("Found {Count} video items to scan", items.Count);

        int processed = 0;
        int generated = 0;

        foreach (var item in items)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                // Look for subtitle files
                var subtitlePath = FindSubtitleFile(item);
                if (string.IsNullOrEmpty(subtitlePath))
                {
                    processed++;
                    progress.Report((double)processed / items.Count * 100);
                    continue;
                }

                _logger.LogDebug("Processing subtitles for: {Name}", item.Name);

                // Read subtitle file
                var subtitleContent = await File.ReadAllTextAsync(subtitlePath, cancellationToken);
                
                // Parse based on extension
                List<Services.SubtitleEntry> entries;
                if (subtitlePath.EndsWith(".srt", StringComparison.OrdinalIgnoreCase))
                {
                    entries = parser.ParseSrt(subtitleContent);
                }
                else if (subtitlePath.EndsWith(".vtt", StringComparison.OrdinalIgnoreCase))
                {
                    entries = parser.ParseVtt(subtitleContent);
                }
                else
                {
                    processed++;
                    progress.Report((double)processed / items.Count * 100);
                    continue;
                }

                // Detect profanity
                var matches = detector.ScanSubtitles(entries);
                
                if (matches.Count > 0)
                {
                    // Generate mute ranges
                    var muteRanges = generator.GenerateMuteRanges(
                        matches,
                        config.MutePaddingMs,
                        config.MuteEntireSentence);

                    // Generate and save metadata
                    var metadataJson = generator.GenerateMetadataJson(muteRanges);
                    var metadataPath = Path.ChangeExtension(item.Path, ".profanity.json");
                    await File.WriteAllTextAsync(metadataPath, metadataJson, cancellationToken);

                    _logger.LogInformation(
                        "Generated profanity filter for {Name}: {Count} mute ranges",
                        item.Name,
                        muteRanges.Count);
                    generated++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing item: {Name}", item.Name);
            }

            processed++;
            progress.Report((double)processed / items.Count * 100);
        }

        _logger.LogInformation(
            "Profanity filter scan complete. Processed: {Processed}, Generated: {Generated}",
            processed,
            generated);
    }

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        // Run weekly by default
        return new[]
        {
            new TaskTriggerInfo
            {
                Type = TaskTriggerInfoType.DailyTrigger,
                DayOfWeek = DayOfWeek.Sunday,
                TimeOfDayTicks = TimeSpan.FromHours(2).Ticks
            }
        };
    }

    private string? FindSubtitleFile(BaseItem item)
    {
        if (string.IsNullOrEmpty(item.Path))
        {
            return null;
        }

        var directory = Path.GetDirectoryName(item.Path);
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(item.Path);

        if (string.IsNullOrEmpty(directory))
        {
            return null;
        }

        // Check for external subtitle files in movie directory
        var subtitleExtensions = new[] { ".srt", ".vtt" };
        foreach (var ext in subtitleExtensions)
        {
            var subtitlePath = Path.Combine(directory, fileNameWithoutExtension + ext);
            if (File.Exists(subtitlePath))
            {
                _logger.LogDebug("Found external subtitle: {Path}", subtitlePath);
                return subtitlePath;
            }

            // Also check for language-specific subtitles (e.g., movie.en.srt)
            subtitlePath = Path.Combine(directory, fileNameWithoutExtension + ".en" + ext);
            if (File.Exists(subtitlePath))
            {
                _logger.LogDebug("Found language-specific subtitle: {Path}", subtitlePath);
                return subtitlePath;
            }
        }

        // Check Jellyfin's subtitle cache for extracted embedded subtitles
        // Subtitle extractor plugin stores them as: /data/subtitles/{userId}/{itemId}.srt
        var dataPath = _appPaths.DataPath;
        var subtitlesPath = Path.Combine(dataPath, "subtitles");
        
        if (Directory.Exists(subtitlesPath))
        {
            // Check all user subtitle directories
            foreach (var userDir in Directory.GetDirectories(subtitlesPath))
            {
                foreach (var ext in subtitleExtensions)
                {
                    // Check for subtitle file named with item's ID
                    var cachedSubtitle = Path.Combine(userDir, item.Id.ToString("N") + ext);
                    if (File.Exists(cachedSubtitle))
                    {
                        _logger.LogDebug("Found cached subtitle for {Name}: {Path}", item.Name, cachedSubtitle);
                        return cachedSubtitle;
                    }
                }
            }
        }

        _logger.LogDebug("No subtitle found for: {Name}", item.Name);
        return null;
    }
}
