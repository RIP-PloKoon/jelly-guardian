using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Jellyfin.Plugin.ProfanityFilter.Services;

/// <summary>
/// Service for detecting profanity in subtitle text with grammatical awareness.
/// </summary>
public class ProfanityDetector
{
    private readonly HashSet<string> _profanityWords;
    private readonly Dictionary<string, string> _wordReplacements;
    private readonly Dictionary<string, Regex> _profanityPatterns;
    private readonly bool _useGrammaticalReplacement;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProfanityDetector"/> class.
    /// </summary>
    /// <param name="profanityWords">Comma-separated list of profanity words (optional, will load from embedded resource if empty).</param>
    /// <param name="enableWordReplacement">Whether to enable word replacement (e.g., "shit" -> "poop").</param>
    /// <param name="useGrammaticalReplacement">Whether to use context-aware grammatical replacement.</param>
    public ProfanityDetector(string profanityWords = "", bool enableWordReplacement = false, bool useGrammaticalReplacement = false)
    {
        _profanityWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        _wordReplacements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        _useGrammaticalReplacement = useGrammaticalReplacement;
        
        // Load from embedded resource if no words provided
        if (string.IsNullOrWhiteSpace(profanityWords))
        {
            LoadFromEmbeddedResource(enableWordReplacement);
        }
        else
        {
            // Load from configuration
            foreach (var word in profanityWords.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = word.Trim();
                if (!string.IsNullOrWhiteSpace(trimmed))
                {
                    _profanityWords.Add(trimmed.ToLowerInvariant());
                }
            }
        }

        // Build regex patterns
        _profanityPatterns = new Dictionary<string, Regex>();
        foreach (var word in _profanityWords)
        {
            // Create regex pattern that matches the word with word boundaries
            // Enhanced to catch compound words like "shitload", "goddamn", "asshole"
            var pattern = @"\b" + Regex.Escape(word).Replace(@"\*", ".?") + @"(s|ed|ing|er|hole|load|damn)?\b";
            _profanityPatterns[word] = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
        }
    }

    /// <summary>
    /// Load profanity words from embedded resource file.
    /// </summary>
    /// <param name="loadReplacements">Whether to load word replacements.</param>
    private void LoadFromEmbeddedResource(bool loadReplacements)
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourcePath = "Jellyfin.Plugin.ProfanityFilter.Resources.swears.txt";
            
            using var stream = assembly.GetManifestResourceStream(resourcePath);
            if (stream == null)
            {
                // Fallback to default list if resource not found
                LoadDefaultWords();
                return;
            }

            using var reader = new StreamReader(stream);
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                // Skip comments and empty lines
                if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("#"))
                {
                    continue;
                }

                // Check for word|replacement format
                var parts = line.Split('|', 2);
                var word = parts[0].Trim().ToLowerInvariant();
                
                if (!string.IsNullOrWhiteSpace(word))
                {
                    _profanityWords.Add(word);
                    
                    if (loadReplacements && parts.Length > 1)
                    {
                        var replacement = parts[1].Trim();
                        if (!string.IsNullOrWhiteSpace(replacement))
                        {
                            _wordReplacements[word] = replacement;
                        }
                    }
                }
            }
        }
        catch (Exception)
        {
            // Fallback to default words on any error
            LoadDefaultWords();
        }
    }

    /// <summary>
    /// Load default profanity word list.
    /// </summary>
    private void LoadDefaultWords()
    {
        var defaultWords = new[]
        {
            "fuck", "shit", "ass", "damn", "bitch", "hell", "bastard", "crap",
            "dick", "pussy", "cock", "tits", "motherfucker", "asshole", "bullshit",
            "goddamn", "piss", "cunt", "whore", "slut", "fag", "faggot", "nigger"
        };

        foreach (var word in defaultWords)
        {
            _profanityWords.Add(word.ToLowerInvariant());
        }
    }

    /// <summary>
    /// Get replacement text for a profanity word.
    /// </summary>
    /// <param name="word">The profanity word.</param>
    /// <returns>Replacement text, or "*****" if no mapping exists.</returns>
    public string GetReplacement(string word)
    {
        if (_wordReplacements.TryGetValue(word.ToLowerInvariant(), out var replacement))
        {
            return replacement;
        }

        return "*****";
    }

    /// <summary>
    /// Get replacement text with optional grammatical awareness.
    /// </summary>
    /// <param name="word">The profanity word.</param>
    /// <param name="fullText">Full subtitle text for context.</param>
    /// <param name="wordIndex">Position in text.</param>
    /// <returns>Appropriate replacement.</returns>
    public string GetContextualReplacement(string word, string fullText, int wordIndex)
    {
        if (_useGrammaticalReplacement)
        {
            return GrammaticalClassifier.ReplaceWithContext(word, fullText, wordIndex);
        }

        return GetReplacement(word);
    }

    /// <summary>
    /// Detect profanity in a subtitle entry.
    /// </summary>
    /// <param name="entry">The subtitle entry to check.</param>
    /// <returns>List of detected profanity matches with timing.</returns>
    public List<ProfanityMatch> DetectProfanity(SubtitleEntry entry)
    {
        var matches = new List<ProfanityMatch>();
        var text = entry.Text;

        foreach (var pattern in _profanityPatterns)
        {
            var regexMatches = pattern.Value.Matches(text);
            foreach (Match match in regexMatches)
            {
                // Calculate approximate timing within the subtitle entry
                // Assumes even distribution of words across the time span
                var wordPosition = (double)match.Index / text.Length;
                var duration = entry.EndTime - entry.StartTime;
                var wordStart = entry.StartTime + TimeSpan.FromMilliseconds(duration.TotalMilliseconds * wordPosition);
                
                // Estimate word duration (average word is ~300ms)
                var wordDuration = TimeSpan.FromMilliseconds(Math.Min(300, duration.TotalMilliseconds * match.Length / text.Length));
                
                // Get contextually appropriate replacement
                var replacement = GetContextualReplacement(match.Value, text, match.Index);
                
                matches.Add(new ProfanityMatch
                {
                    Word = match.Value,
                    Replacement = replacement,
                    StartTime = wordStart,
                    EndTime = wordStart + wordDuration,
                    SentenceStart = entry.StartTime,
                    SentenceEnd = entry.EndTime
                });
            }
        }

        return matches;
    }

    /// <summary>
    /// Scan entire subtitle list for profanity.
    /// </summary>
    /// <param name="entries">List of subtitle entries.</param>
    /// <returns>List of all profanity matches.</returns>
    public List<ProfanityMatch> ScanSubtitles(List<SubtitleEntry> entries)
    {
        var allMatches = new List<ProfanityMatch>();
        
        foreach (var entry in entries)
        {
            allMatches.AddRange(DetectProfanity(entry));
        }

        return allMatches;
    }

    /// <summary>
    /// Gets the count of loaded profanity words.
    /// </summary>
    public int WordCount => _profanityWords.Count;
}

/// <summary>
/// Represents a detected profanity match.
/// </summary>
public class ProfanityMatch
{
    /// <summary>
    /// Gets or sets the detected word.
    /// </summary>
    public required string Word { get; set; }

    /// <summary>
    /// Gets or sets the replacement text for the word.
    /// </summary>
    public string Replacement { get; set; } = "*****";

    /// <summary>
    /// Gets or sets the estimated start time of the word.
    /// </summary>
    public TimeSpan StartTime { get; set; }

    /// <summary>
    /// Gets or sets the estimated end time of the word.
    /// </summary>
    public TimeSpan EndTime { get; set; }

    /// <summary>
    /// Gets or sets the start time of the containing sentence/subtitle.
    /// </summary>
    public TimeSpan SentenceStart { get; set; }

    /// <summary>
    /// Gets or sets the end time of the containing sentence/subtitle.
    /// </summary>
    public TimeSpan SentenceEnd { get; set; }
}
