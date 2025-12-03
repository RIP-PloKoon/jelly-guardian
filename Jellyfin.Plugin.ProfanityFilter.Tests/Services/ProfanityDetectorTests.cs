using System;
using System.Linq;
using Xunit;
using Jellyfin.Plugin.ProfanityFilter.Services;

namespace Jellyfin.Plugin.ProfanityFilter.Tests.Services;

public class ProfanityDetectorTests
{
    [Fact]
    public void DetectProfanity_FindsExactWord()
    {
        // Arrange
        var detector = new ProfanityDetector("fuck,shit,damn");
        var entry = new SubtitleEntry
        {
            StartTime = TimeSpan.FromSeconds(10),
            EndTime = TimeSpan.FromSeconds(15),
            Text = "This is a fucking test"
        };

        // Act
        var matches = detector.DetectProfanity(entry);

        // Assert
        Assert.Single(matches);
        Assert.Equal("fucking", matches[0].Word.ToLower());
    }

    [Fact]
    public void DetectProfanity_FindsMultipleWords()
    {
        // Arrange
        var detector = new ProfanityDetector("fuck,shit,damn");
        var entry = new SubtitleEntry
        {
            StartTime = TimeSpan.FromSeconds(10),
            EndTime = TimeSpan.FromSeconds(15),
            Text = "This shit is fucking terrible, damn it!"
        };

        // Act
        var matches = detector.DetectProfanity(entry);

        // Assert
        Assert.Equal(3, matches.Count);
        Assert.Contains(matches, m => m.Word.ToLower().Contains("shit"));
        Assert.Contains(matches, m => m.Word.ToLower().Contains("fuck"));
        Assert.Contains(matches, m => m.Word.ToLower().Contains("damn"));
    }

    [Fact]
    public void DetectProfanity_IgnoresPartialMatches()
    {
        // Arrange
        var detector = new ProfanityDetector("ass");
        var entry = new SubtitleEntry
        {
            StartTime = TimeSpan.FromSeconds(10),
            EndTime = TimeSpan.FromSeconds(15),
            Text = "They passed the class with flying colors"
        };

        // Act
        var matches = detector.DetectProfanity(entry);

        // Assert
        Assert.Empty(matches);
    }

    [Fact]
    public void DetectProfanity_FindsWordVariations()
    {
        // Arrange
        var detector = new ProfanityDetector("fuck");
        var entry = new SubtitleEntry
        {
            StartTime = TimeSpan.FromSeconds(10),
            EndTime = TimeSpan.FromSeconds(15),
            Text = "fuck, fucked, fucking, fucker"
        };

        // Act
        var matches = detector.DetectProfanity(entry);

        // Assert
        Assert.Equal(4, matches.Count);
    }

    [Fact]
    public void DetectProfanity_CaseInsensitive()
    {
        // Arrange
        var detector = new ProfanityDetector("fuck");
        var entry = new SubtitleEntry
        {
            StartTime = TimeSpan.FromSeconds(10),
            EndTime = TimeSpan.FromSeconds(15),
            Text = "FUCK, Fuck, FuCk, fuck"
        };

        // Act
        var matches = detector.DetectProfanity(entry);

        // Assert
        Assert.Equal(4, matches.Count);
    }

    [Fact]
    public void ScanSubtitles_ProcessesMultipleEntries()
    {
        // Arrange
        var detector = new ProfanityDetector("fuck,shit");
        var entries = new[]
        {
            new SubtitleEntry
            {
                StartTime = TimeSpan.FromSeconds(10),
                EndTime = TimeSpan.FromSeconds(15),
                Text = "This is clean"
            },
            new SubtitleEntry
            {
                StartTime = TimeSpan.FromSeconds(20),
                EndTime = TimeSpan.FromSeconds(25),
                Text = "This has fuck in it"
            },
            new SubtitleEntry
            {
                StartTime = TimeSpan.FromSeconds(30),
                EndTime = TimeSpan.FromSeconds(35),
                Text = "This has shit in it"
            }
        };

        // Act
        var matches = detector.ScanSubtitles(entries.ToList());

        // Assert
        Assert.Equal(2, matches.Count);
    }

    [Fact]
    public void DetectProfanity_CalculatesApproximateTiming()
    {
        // Arrange
        var detector = new ProfanityDetector("fuck");
        var entry = new SubtitleEntry
        {
            StartTime = TimeSpan.FromSeconds(10),
            EndTime = TimeSpan.FromSeconds(15),
            Text = "fuck"
        };

        // Act
        var matches = detector.DetectProfanity(entry);

        // Assert
        Assert.Single(matches);
        Assert.True(matches[0].StartTime >= entry.StartTime);
        Assert.True(matches[0].EndTime <= entry.EndTime);
    }
}
