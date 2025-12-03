using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace Jellyfin.Plugin.ProfanityFilter.Services;

/// <summary>
/// Service for parsing subtitle files and extracting timing/text information.
/// </summary>
public class SubtitleParser
{
    /// <summary>
    /// Parse SRT format subtitle file.
    /// </summary>
    /// <param name="content">The SRT file content.</param>
    /// <returns>List of subtitle entries.</returns>
    public List<SubtitleEntry> ParseSrt(string content)
    {
        var entries = new List<SubtitleEntry>();
        var blocks = content.Split(new[] { "\r\n\r\n", "\n\n" }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var block in blocks)
        {
            var lines = block.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            if (lines.Length < 3)
            {
                continue;
            }

            // Parse timing line (format: 00:00:20,000 --> 00:00:24,400)
            var timingMatch = Regex.Match(lines[1], @"(\d{2}:\d{2}:\d{2},\d{3})\s*-->\s*(\d{2}:\d{2}:\d{2},\d{3})");
            if (!timingMatch.Success)
            {
                continue;
            }

            var startTime = ParseSrtTimestamp(timingMatch.Groups[1].Value);
            var endTime = ParseSrtTimestamp(timingMatch.Groups[2].Value);

            // Combine remaining lines as text
            var text = string.Join(" ", lines.Skip(2).Where(l => !string.IsNullOrWhiteSpace(l)));

            entries.Add(new SubtitleEntry
            {
                StartTime = startTime,
                EndTime = endTime,
                Text = text
            });
        }

        return entries;
    }

    /// <summary>
    /// Parse VTT format subtitle file.
    /// </summary>
    /// <param name="content">The VTT file content.</param>
    /// <returns>List of subtitle entries.</returns>
    public List<SubtitleEntry> ParseVtt(string content)
    {
        var entries = new List<SubtitleEntry>();
        
        // Skip WEBVTT header
        var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        var inCue = false;
        TimeSpan? startTime = null;
        TimeSpan? endTime = null;
        var textLines = new List<string>();

        foreach (var line in lines)
        {
            // Skip WEBVTT header and empty lines at the start
            if (line.StartsWith("WEBVTT", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Check for timing line (format: 00:00:20.000 --> 00:00:24.400)
            var timingMatch = Regex.Match(line, @"(\d{2}:\d{2}:\d{2}\.\d{3})\s*-->\s*(\d{2}:\d{2}:\d{2}\.\d{3})");
            if (timingMatch.Success)
            {
                // Save previous cue if exists
                if (startTime.HasValue && endTime.HasValue && textLines.Count > 0)
                {
                    entries.Add(new SubtitleEntry
                    {
                        StartTime = startTime.Value,
                        EndTime = endTime.Value,
                        Text = string.Join(" ", textLines)
                    });
                }

                startTime = ParseVttTimestamp(timingMatch.Groups[1].Value);
                endTime = ParseVttTimestamp(timingMatch.Groups[2].Value);
                textLines.Clear();
                inCue = true;
                continue;
            }

            // Empty line marks end of cue
            if (string.IsNullOrWhiteSpace(line))
            {
                if (inCue && startTime.HasValue && endTime.HasValue && textLines.Count > 0)
                {
                    entries.Add(new SubtitleEntry
                    {
                        StartTime = startTime.Value,
                        EndTime = endTime.Value,
                        Text = string.Join(" ", textLines)
                    });
                    textLines.Clear();
                    inCue = false;
                }
                continue;
            }

            // Collect text lines
            if (inCue)
            {
                textLines.Add(line);
            }
        }

        // Add last cue if exists
        if (startTime.HasValue && endTime.HasValue && textLines.Count > 0)
        {
            entries.Add(new SubtitleEntry
            {
                StartTime = startTime.Value,
                EndTime = endTime.Value,
                Text = string.Join(" ", textLines)
            });
        }

        return entries;
    }

    private TimeSpan ParseSrtTimestamp(string timestamp)
    {
        // Format: 00:00:20,000
        var parts = timestamp.Split(':');
        var secondsParts = parts[2].Split(',');
        
        return new TimeSpan(
            0,
            int.Parse(parts[0], CultureInfo.InvariantCulture),
            int.Parse(parts[1], CultureInfo.InvariantCulture),
            int.Parse(secondsParts[0], CultureInfo.InvariantCulture),
            int.Parse(secondsParts[1], CultureInfo.InvariantCulture));
    }

    private TimeSpan ParseVttTimestamp(string timestamp)
    {
        // Format: 00:00:20.000
        var parts = timestamp.Split(':');
        var secondsParts = parts[2].Split('.');
        
        return new TimeSpan(
            0,
            int.Parse(parts[0], CultureInfo.InvariantCulture),
            int.Parse(parts[1], CultureInfo.InvariantCulture),
            int.Parse(secondsParts[0], CultureInfo.InvariantCulture),
            int.Parse(secondsParts[1], CultureInfo.InvariantCulture));
    }
}

/// <summary>
/// Represents a single subtitle entry.
/// </summary>
public class SubtitleEntry
{
    /// <summary>
    /// Gets or sets the start time.
    /// </summary>
    public TimeSpan StartTime { get; set; }

    /// <summary>
    /// Gets or sets the end time.
    /// </summary>
    public TimeSpan EndTime { get; set; }

    /// <summary>
    /// Gets or sets the subtitle text.
    /// </summary>
    public string Text { get; set; } = string.Empty;
}
