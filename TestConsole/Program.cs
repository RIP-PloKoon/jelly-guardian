using System;
using System.IO;
using Jellyfin.Plugin.ProfanityFilter.Services;

namespace TestConsole;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("=== Jellyfin Profanity Filter - Test Console ===\n");

        // Test 1: Parse sample SRT
        Console.WriteLine("Test 1: Parsing SRT subtitle file...");
        var srtSample = CreateSampleSrt();
        File.WriteAllText("sample.srt", srtSample);
        
        var parser = new SubtitleParser();
        var entries = parser.ParseSrt(srtSample);
        Console.WriteLine($"✓ Parsed {entries.Count} subtitle entries");
        
        // Test 2: Detect profanity
        Console.WriteLine("\nTest 2: Detecting profanity...");
        var detector = new ProfanityDetector("fuck,shit,damn,ass,bitch,hell");
        var matches = detector.ScanSubtitles(entries);
        Console.WriteLine($"✓ Found {matches.Count} profanity matches");
        
        foreach (var match in matches)
        {
            Console.WriteLine($"  - '{match.Word}' at {match.StartTime.TotalSeconds:F2}s");
        }
        
        // Test 3: Generate mute ranges
        Console.WriteLine("\nTest 3: Generating mute ranges...");
        var generator = new MuteTimestampGenerator();
        
        // Test with word-only muting
        var wordRanges = generator.GenerateMuteRanges(matches, paddingMs: 100, muteEntireSentence: false);
        Console.WriteLine($"✓ Generated {wordRanges.Count} mute ranges (word-only mode)");
        
        foreach (var range in wordRanges)
        {
            Console.WriteLine($"  - {range.StartMs}ms to {range.EndMs}ms ({range.Word})");
        }
        
        // Test with sentence muting
        var sentenceRanges = generator.GenerateMuteRanges(matches, paddingMs: 100, muteEntireSentence: true);
        Console.WriteLine($"\n✓ Generated {sentenceRanges.Count} mute ranges (sentence mode)");
        
        // Test 4: Generate JSON metadata
        Console.WriteLine("\nTest 4: Generating JSON metadata...");
        var json = generator.GenerateMetadataJson(wordRanges);
        File.WriteAllText("sample.profanity.json", json);
        Console.WriteLine("✓ JSON metadata saved to sample.profanity.json");
        Console.WriteLine("\nMetadata preview:");
        Console.WriteLine(json);
        
        // Test 5: Test VTT parsing
        Console.WriteLine("\n\nTest 5: Parsing VTT subtitle file...");
        var vttSample = CreateSampleVtt();
        File.WriteAllText("sample.vtt", vttSample);
        
        var vttEntries = parser.ParseVtt(vttSample);
        Console.WriteLine($"✓ Parsed {vttEntries.Count} VTT entries");
        
        var vttMatches = detector.ScanSubtitles(vttEntries);
        Console.WriteLine($"✓ Found {vttMatches.Count} profanity matches in VTT");
        
        // Test 6: Edge cases
        Console.WriteLine("\nTest 6: Testing edge cases...");
        TestEdgeCases(detector);
        
        Console.WriteLine("\n=== All Tests Complete ===");
        Console.WriteLine("\nFiles created:");
        Console.WriteLine("  - sample.srt");
        Console.WriteLine("  - sample.vtt");
        Console.WriteLine("  - sample.profanity.json");
    }
    
    static string CreateSampleSrt()
    {
        return @"1
00:00:10,000 --> 00:00:15,000
Welcome to the movie, everything is clean here.

2
00:00:20,000 --> 00:00:24,000
Oh shit, that was unexpected!

3
00:00:30,000 --> 00:00:35,000
What the fuck is going on here?

4
00:00:40,000 --> 00:00:45,000
Damn it, this is getting worse.

5
00:00:50,000 --> 00:00:55,000
Just a normal sentence without issues.

6
00:01:00,000 --> 00:01:05,000
You're such a bitch for doing that.

7
00:01:10,000 --> 00:01:15,000
Multiple words: fuck, shit, and damn all together!

8
00:01:20,000 --> 00:01:25,000
Everything is fine now.
";
    }
    
    static string CreateSampleVtt()
    {
        return @"WEBVTT

00:00:10.000 --> 00:00:15.000
Clean dialogue here.

00:00:20.000 --> 00:00:24.000
Holy shit, that scared me!

00:00:30.000 --> 00:00:35.000
What the hell is happening?

00:00:40.000 --> 00:00:45.000
More clean dialogue.
";
    }
    
    static void TestEdgeCases(ProfanityDetector detector)
    {
        // Test case sensitivity
        var entry1 = new SubtitleEntry
        {
            StartTime = TimeSpan.FromSeconds(10),
            EndTime = TimeSpan.FromSeconds(15),
            Text = "FUCK, Fuck, FuCk, fuck"
        };
        var matches1 = detector.DetectProfanity(entry1);
        Console.WriteLine($"✓ Case insensitivity: Found {matches1.Count} matches in 'FUCK, Fuck, FuCk, fuck'");
        
        // Test word boundaries
        var entry2 = new SubtitleEntry
        {
            StartTime = TimeSpan.FromSeconds(10),
            EndTime = TimeSpan.FromSeconds(15),
            Text = "They passed the class with assistance"
        };
        var matches2 = detector.DetectProfanity(entry2);
        Console.WriteLine($"✓ Word boundaries: Found {matches2.Count} matches in 'passed/assistance' (should be 0)");
        
        // Test variations
        var entry3 = new SubtitleEntry
        {
            StartTime = TimeSpan.FromSeconds(10),
            EndTime = TimeSpan.FromSeconds(15),
            Text = "fuck, fucked, fucking, fucker"
        };
        var matches3 = detector.DetectProfanity(entry3);
        Console.WriteLine($"✓ Word variations: Found {matches3.Count} matches in 'fuck, fucked, fucking, fucker'");
        
        // Test empty
        var entry4 = new SubtitleEntry
        {
            StartTime = TimeSpan.FromSeconds(10),
            EndTime = TimeSpan.FromSeconds(15),
            Text = ""
        };
        var matches4 = detector.DetectProfanity(entry4);
        Console.WriteLine($"✓ Empty string: Found {matches4.Count} matches (should be 0)");
    }
}
