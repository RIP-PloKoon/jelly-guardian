using System;
using System.Collections.Generic;
using System.Linq;

namespace Jellyfin.Plugin.ProfanityFilter.Services;

/// <summary>
/// Service for generating mute timestamps from profanity matches.
/// </summary>
public class MuteTimestampGenerator
{
    /// <summary>
    /// Generate mute timestamps with padding.
    /// </summary>
    /// <param name="matches">List of profanity matches.</param>
    /// <param name="paddingMs">Padding in milliseconds before and after each match.</param>
    /// <param name="muteEntireSentence">Whether to mute entire sentence or just the word.</param>
    /// <returns>List of mute ranges.</returns>
    public List<MuteRange> GenerateMuteRanges(
        List<ProfanityMatch> matches,
        int paddingMs = 100,
        bool muteEntireSentence = false)
    {
        var ranges = new List<MuteRange>();
        var padding = TimeSpan.FromMilliseconds(paddingMs);

        foreach (var match in matches)
        {
            TimeSpan start, end;

            if (muteEntireSentence)
            {
                // Mute the entire subtitle entry
                start = match.SentenceStart;
                end = match.SentenceEnd;
            }
            else
            {
                // Mute just the word with padding
                start = match.StartTime - padding;
                end = match.EndTime + padding;

                // Ensure we don't go negative or beyond sentence bounds
                if (start < match.SentenceStart)
                {
                    start = match.SentenceStart;
                }

                if (end > match.SentenceEnd)
                {
                    end = match.SentenceEnd;
                }
            }

            ranges.Add(new MuteRange
            {
                StartMs = (long)start.TotalMilliseconds,
                EndMs = (long)end.TotalMilliseconds,
                Word = match.Word
            });
        }

        // Merge overlapping ranges
        return MergeOverlappingRanges(ranges);
    }

    /// <summary>
    /// Merge overlapping mute ranges to avoid redundant muting.
    /// </summary>
    /// <param name="ranges">List of mute ranges.</param>
    /// <returns>List of merged ranges.</returns>
    private List<MuteRange> MergeOverlappingRanges(List<MuteRange> ranges)
    {
        if (ranges.Count == 0)
        {
            return ranges;
        }

        // Sort by start time
        var sorted = ranges.OrderBy(r => r.StartMs).ToList();
        var merged = new List<MuteRange> { sorted[0] };

        for (int i = 1; i < sorted.Count; i++)
        {
            var current = sorted[i];
            var last = merged[merged.Count - 1];

            // Check if current overlaps with last
            if (current.StartMs <= last.EndMs)
            {
                // Merge: extend the end time and combine words
                last.EndMs = Math.Max(last.EndMs, current.EndMs);
                if (!last.Word.Contains(current.Word))
                {
                    last.Word += ", " + current.Word;
                }
            }
            else
            {
                // No overlap, add as new range
                merged.Add(current);
            }
        }

        return merged;
    }

    /// <summary>
    /// Generate JSON metadata for client-side consumption.
    /// </summary>
    /// <param name="ranges">List of mute ranges.</param>
    /// <returns>JSON string containing mute metadata.</returns>
    public string GenerateMetadataJson(List<MuteRange> ranges)
    {
        var rangesJson = string.Join(",\n    ", ranges.Select(r => 
            $"{{ \"start\": {r.StartMs}, \"end\": {r.EndMs}, \"word\": \"{r.Word}\" }}"));

        return $@"{{
  ""version"": ""1.0"",
  ""muteRanges"": [
    {rangesJson}
  ]
}}";
    }
}

/// <summary>
/// Represents a time range to mute.
/// </summary>
public class MuteRange
{
    /// <summary>
    /// Gets or sets the start time in milliseconds.
    /// </summary>
    public long StartMs { get; set; }

    /// <summary>
    /// Gets or sets the end time in milliseconds.
    /// </summary>
    public long EndMs { get; set; }

    /// <summary>
    /// Gets or sets the word(s) being muted.
    /// </summary>
    public string Word { get; set; } = string.Empty;
}
