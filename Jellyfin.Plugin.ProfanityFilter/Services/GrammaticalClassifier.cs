using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;

namespace Jellyfin.Plugin.ProfanityFilter.Services;

/// <summary>
/// Lightweight part-of-speech classifier for profanity replacement.
/// Uses pattern matching and context rules without external NLP libraries.
/// </summary>
public class GrammaticalClassifier
{
    /// <summary>
    /// Grammatical classes for words.
    /// </summary>
    public enum WordClass
    {
        Adjective,
        Noun,
        Verb,
        Adverb,
        Unknown
    }

    private static readonly Dictionary<string, Dictionary<WordClass, string[]>> _synonymsByClass = new()
    {
        ["shit"] = new()
        {
            [WordClass.Noun] = new[] { "nonsense", "garbage", "junk", "rubbish" },
            [WordClass.Adjective] = new[] { "crummy", "lousy", "terrible", "awful" },
            [WordClass.Verb] = new[] { "deceive", "mislead", "fool" },
            [WordClass.Adverb] = new[] { "very", "really", "extremely" }
        },
        ["fuck"] = new()
        {
            [WordClass.Noun] = new[] { "heck", "darn" },
            [WordClass.Adjective] = new[] { "darn", "freaking", "terrible" },
            [WordClass.Verb] = new[] { "mess up", "ruin", "damage" },
            [WordClass.Adverb] = new[] { "very", "extremely", "really" }
        },
        ["damn"] = new()
        {
            [WordClass.Noun] = new[] { "darn", "heck" },
            [WordClass.Adjective] = new[] { "dang", "darn", "blasted" },
            [WordClass.Verb] = new[] { "condemn", "criticize" },
            [WordClass.Adverb] = new[] { "very", "really", "quite" }
        },
        ["ass"] = new()
        {
            [WordClass.Noun] = new[] { "butt", "rear", "behind" },
            [WordClass.Adjective] = new[] { "foolish", "stupid", "ridiculous" }
        },
        ["bastard"] = new()
        {
            [WordClass.Noun] = new[] { "jerk", "creep", "scoundrel" },
            [WordClass.Adjective] = new[] { "terrible", "awful", "nasty" }
        },
        ["bitch"] = new()
        {
            [WordClass.Noun] = new[] { "person", "complainer", "grump" },
            [WordClass.Verb] = new[] { "complain", "gripe", "grumble" },
            [WordClass.Adjective] = new[] { "difficult", "unpleasant", "mean" }
        },
        ["hell"] = new()
        {
            [WordClass.Noun] = new[] { "heck", "trouble", "chaos" },
            [WordClass.Adjective] = new[] { "terrible", "awful", "horrible" },
            [WordClass.Adverb] = new[] { "very", "extremely", "really" }
        },
        ["crap"] = new()
        {
            [WordClass.Noun] = new[] { "nonsense", "junk", "garbage" },
            [WordClass.Adjective] = new[] { "lousy", "poor", "bad" },
            [WordClass.Verb] = new[] { "fail", "mess up" }
        }
    };

    // Patterns for detecting grammatical class based on context
    private static readonly Dictionary<WordClass, Regex[]> _contextPatterns = new()
    {
        // Adjective: "a/an/the <adj> <noun>", "so/very/really <adj>", "<adj> idea/person/thing"
        [WordClass.Adjective] = new[]
        {
            new Regex(@"\b(?:a|an|the|this|that|some)\s+\w+\s+(?:idea|person|thing|day|time|movie|show|place)", RegexOptions.IgnoreCase),
            new Regex(@"\b(?:so|very|really|pretty|quite|too)\s+\w+", RegexOptions.IgnoreCase),
            new Regex(@"\w+\s+(?:as|thing|idea|person|movie|place|day|time)", RegexOptions.IgnoreCase)
        },
        
        // Noun: "the/this/that <noun>", "<noun> is/was/are", "of <noun>"
        [WordClass.Noun] = new[]
        {
            new Regex(@"\b(?:a|an|the|this|that|some|what|such)\s+\w+\s*(?:is|was|are|were|of|in|on|at)?", RegexOptions.IgnoreCase),
            new Regex(@"\w+\s+(?:is|was|are|were|has|have|had)", RegexOptions.IgnoreCase),
            new Regex(@"(?:of|about|for|with)\s+\w+", RegexOptions.IgnoreCase)
        },
        
        // Verb: "<verb>ing", "<verb>ed", "to <verb>", "can/will/would <verb>"
        [WordClass.Verb] = new[]
        {
            new Regex(@"\w+(?:ing|ed)\b", RegexOptions.IgnoreCase),
            new Regex(@"\b(?:to|can|will|would|should|could|must|may|might)\s+\w+", RegexOptions.IgnoreCase),
            new Regex(@"\b(?:I|you|he|she|it|we|they|who)\s+\w+", RegexOptions.IgnoreCase)
        },
        
        // Adverb: "<adverb> <adjective>", "<adverb> <verb>", "so <adverb>"
        [WordClass.Adverb] = new[]
        {
            new Regex(@"\w+\s+(?:right|good|bad|wrong|sure|true|quick|slow|fast)", RegexOptions.IgnoreCase),
            new Regex(@"\b(?:so|too|very)\s+\w+\s+(?:right|good|bad)", RegexOptions.IgnoreCase)
        }
    };

    /// <summary>
    /// Classify the grammatical role of a word based on surrounding context.
    /// </summary>
    /// <param name="word">The word to classify.</param>
    /// <param name="fullText">The complete sentence/subtitle text.</param>
    /// <param name="wordIndex">Position of the word in the text.</param>
    /// <returns>The detected grammatical class.</returns>
    public static WordClass ClassifyWord(string word, string fullText, int wordIndex)
    {
        // Extract context window (5 words before and after)
        var contextStart = Math.Max(0, wordIndex - 100);
        var contextEnd = Math.Min(fullText.Length, wordIndex + word.Length + 100);
        var context = fullText.Substring(contextStart, contextEnd - contextStart);

        // Common verb endings
        if (word.EndsWith("ing") || word.EndsWith("ed") || word.EndsWith("es"))
        {
            return WordClass.Verb;
        }

        // Check for "ly" ending (common adverb marker)
        if (word.EndsWith("ly"))
        {
            return WordClass.Adverb;
        }

        // Try to match against context patterns
        foreach (var kvp in _contextPatterns)
        {
            foreach (var pattern in kvp.Value)
            {
                if (pattern.IsMatch(context))
                {
                    return kvp.Key;
                }
            }
        }

        // Default heuristics based on position
        var wordsBeforeMatch = Regex.Match(fullText.Substring(0, wordIndex), @"(\w+)\s*$");
        if (wordsBeforeMatch.Success)
        {
            var prevWord = wordsBeforeMatch.Groups[1].Value.ToLower();
            
            // Articles/determiners before word → likely noun or adjective
            if (new[] { "a", "an", "the", "this", "that", "my", "your", "his", "her" }.Any(x => x == prevWord))
            {
                // Check if there's another word after (adj + noun pattern)
                var wordsAfterMatch = Regex.Match(fullText.Substring(wordIndex + word.Length), @"^\s*(\w+)");
                if (wordsAfterMatch.Success && !new[] { "is", "was", "are", "were" }.Any(x => x == wordsAfterMatch.Groups[1].Value.ToLower()))
                {
                    return WordClass.Adjective;
                }
                return WordClass.Noun;
            }

            // Intensifiers before word → likely adjective or adverb
            if (new[] { "so", "very", "really", "pretty", "quite", "too" }.Any(x => x == prevWord))
            {
                return WordClass.Adjective;
            }

            // "to" before word → likely verb
            if (prevWord == "to")
            {
                return WordClass.Verb;
            }
        }

        // Default to noun if uncertain
        return WordClass.Noun;
    }

    /// <summary>
    /// Get appropriate synonym for a word based on its grammatical class.
    /// </summary>
    /// <param name="word">The profanity word.</param>
    /// <param name="wordClass">The grammatical class.</param>
    /// <returns>A contextually appropriate synonym, or the default replacement.</returns>
    public static string GetContextualReplacement(string word, WordClass wordClass)
    {
        var normalizedWord = word.ToLower()
            .TrimEnd('s', 'd', 'g', 'r', 'y') // Remove common endings
            .TrimEnd('e', 'i', 'n'); // Handle "ing", "ed" etc

        if (_synonymsByClass.TryGetValue(normalizedWord, out var classSynonyms))
        {
            if (classSynonyms.TryGetValue(wordClass, out var synonyms) && synonyms.Length > 0)
            {
                return synonyms[0]; // Return first synonym for this class
            }
        }

        // Fallback to basic replacement
        return "*****";
    }

    /// <summary>
    /// Detect and replace profanity with grammatically appropriate synonyms.
    /// </summary>
    /// <param name="word">The detected profanity.</param>
    /// <param name="fullText">The complete subtitle text.</param>
    /// <param name="wordIndex">Position of the word.</param>
    /// <returns>Contextually appropriate replacement word.</returns>
    public static string ReplaceWithContext(string word, string fullText, int wordIndex)
    {
        var wordClass = ClassifyWord(word, fullText, wordIndex);
        return GetContextualReplacement(word, wordClass);
    }
}
