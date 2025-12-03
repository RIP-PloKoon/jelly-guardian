using Jellyfin.Plugin.ProfanityFilter.Services;
using Jellyfin.Plugin.ProfanityFilter.Configuration;
using System.Linq;

namespace MovieTester;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("=== Jellyfin Profanity Filter - Movie Tester ===\n");

        if (args.Length == 0)
        {
            ShowUsage();
            return;
        }

        string subtitlePath = args[0];
        
        if (!File.Exists(subtitlePath))
        {
            Console.WriteLine($"Error: File not found: {subtitlePath}");
            return;
        }

        var config = new PluginConfiguration();
        
        int padding = 100;
        bool muteEntireSentence = false;
        
        if (args.Length > 1 && int.TryParse(args[1], out int customPadding))
        {
            padding = customPadding;
            Console.WriteLine($"Using custom padding: {padding}ms\n");
        }
        
        if (args.Length > 2 && bool.TryParse(args[2], out bool customMute))
        {
            muteEntireSentence = customMute;
            Console.WriteLine($"Mute entire sentence: {muteEntireSentence}\n");
        }

        try
        {
            ProcessSubtitleFile(subtitlePath, config, padding, muteEntireSentence);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing file: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }

    static void ProcessSubtitleFile(string subtitlePath, PluginConfiguration config, int padding, bool muteEntireSentence)
    {
        Console.WriteLine($"Processing: {Path.GetFileName(subtitlePath)}");
        Console.WriteLine($"Full path: {subtitlePath}\n");

        string content = File.ReadAllText(subtitlePath);
        string extension = Path.GetExtension(subtitlePath).ToLower();

        var parser = new SubtitleParser();
        var entries = extension switch
        {
            ".srt" => parser.ParseSrt(content),
            ".vtt" => parser.ParseVtt(content),
            _ => throw new NotSupportedException($"Unsupported subtitle format: {extension}")
        };

        Console.WriteLine($"Parsed {entries.Count} subtitle entries\n");

        if (entries.Count == 0)
        {
            Console.WriteLine("Warning: No subtitle entries found!");
            return;
        }

        var detector = new ProfanityDetector(config.ProfanityWords);
        var matches = detector.ScanSubtitles(entries);

        Console.WriteLine($"Found {matches.Count} profanity instances:\n");
        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

        if (matches.Count == 0)
        {
            Console.WriteLine("✓ No profanity detected! This content is clean.\n");
        }
        else
        {
            foreach (var match in matches)
            {
                var entry = entries.FirstOrDefault(e => e.StartTime == match.StartTime);
                Console.WriteLine($"[{match.StartTime:hh\\:mm\\:ss\\.fff}] \"{match.Word}\"");
                if (entry != null)
                {
                    Console.WriteLine($"  Context: {TruncateContext(entry.Text, match.Word, 60)}");
                }
                Console.WriteLine();
            }
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        }

        var generator = new MuteTimestampGenerator();
        var muteRanges = generator.GenerateMuteRanges(matches, padding, muteEntireSentence);

        Console.WriteLine($"\nMute Ranges (with {padding}ms padding):\n");
        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

        if (muteRanges.Count == 0)
        {
            Console.WriteLine("No mute ranges needed.\n");
        }
        else
        {
            foreach (var range in muteRanges)
            {
                var start = TimeSpan.FromMilliseconds(range.StartMs);
                var end = TimeSpan.FromMilliseconds(range.EndMs);
                var duration = end - start;
                Console.WriteLine($"{start:hh\\:mm\\:ss\\.fff} → {end:hh\\:mm\\:ss\\.fff} ({duration.TotalSeconds:F1}s)");
                Console.WriteLine($"  Words: {range.Word}");
                Console.WriteLine();
            }
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

            double totalMuteSeconds = muteRanges.Sum(r => (r.EndMs - r.StartMs) / 1000.0);
            Console.WriteLine($"\nTotal mute duration: {totalMuteSeconds:F1} seconds");
        }

        string jsonOutput = generator.GenerateMetadataJson(muteRanges);
        string outputPath = Path.ChangeExtension(subtitlePath, ".profanity.json");
        File.WriteAllText(outputPath, jsonOutput);

        Console.WriteLine($"\nMetadata saved to: {outputPath}");
        Console.WriteLine("\n✓ Processing complete!");
        Console.WriteLine("\nYou can now:");
        Console.WriteLine("  1. Review the detected profanity above");
        Console.WriteLine("  2. Check the JSON file for client integration");
        Console.WriteLine("  3. Adjust padding/settings if needed");
    }

    static string TruncateContext(string text, string word, int maxLength)
    {
        if (text.Length <= maxLength)
            return text;

        int wordIndex = text.IndexOf(word, StringComparison.OrdinalIgnoreCase);
        if (wordIndex == -1)
            return text.Substring(0, Math.Min(maxLength, text.Length)) + "...";

        int start = Math.Max(0, wordIndex - (maxLength / 2));
        int length = Math.Min(maxLength, text.Length - start);
        
        string result = text.Substring(start, length);
        
        if (start > 0)
            result = "..." + result;
        if (start + length < text.Length)
            result += "...";
            
        return result;
    }

    static void ShowUsage()
    {
        Console.WriteLine("Usage: MovieTester <subtitle-file> [padding-ms] [mute-entire-sentence]");
        Console.WriteLine();
        Console.WriteLine("Arguments:");
        Console.WriteLine("  subtitle-file          Path to .srt or .vtt subtitle file (required)");
        Console.WriteLine("  padding-ms             Milliseconds to pad before/after profanity (default: 100)");
        Console.WriteLine("  mute-entire-sentence   true/false - mute whole sentence vs just word (default: false)");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  MovieTester movie.srt");
        Console.WriteLine("  MovieTester movie.srt 200");
        Console.WriteLine("  MovieTester movie.srt 150 true");
        Console.WriteLine("  MovieTester /path/to/subtitle.vtt");
        Console.WriteLine();
    }
}
