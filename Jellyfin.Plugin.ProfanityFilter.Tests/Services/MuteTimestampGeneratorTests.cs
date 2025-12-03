using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Jellyfin.Plugin.ProfanityFilter.Services;

namespace Jellyfin.Plugin.ProfanityFilter.Tests.Services;

public class MuteTimestampGeneratorTests
{
    private readonly MuteTimestampGenerator _generator;

    public MuteTimestampGeneratorTests()
    {
        _generator = new MuteTimestampGenerator();
    }

    [Fact]
    public void GenerateMuteRanges_WordOnly_AddsPadding()
    {
        // Arrange
        var matches = new List<ProfanityMatch>
        {
            new ProfanityMatch
            {
                Word = "fuck",
                StartTime = TimeSpan.FromSeconds(10),
                EndTime = TimeSpan.FromSeconds(10.3),
                SentenceStart = TimeSpan.FromSeconds(9),
                SentenceEnd = TimeSpan.FromSeconds(12)
            }
        };

        // Act
        var ranges = _generator.GenerateMuteRanges(matches, paddingMs: 100, muteEntireSentence: false);

        // Assert
        Assert.Single(ranges);
        Assert.Equal(9900, ranges[0].StartMs); // 10000 - 100
        Assert.Equal(10400, ranges[0].EndMs);  // 10300 + 100
    }

    [Fact]
    public void GenerateMuteRanges_EntireSentence_UsesSentenceBounds()
    {
        // Arrange
        var matches = new List<ProfanityMatch>
        {
            new ProfanityMatch
            {
                Word = "fuck",
                StartTime = TimeSpan.FromSeconds(10),
                EndTime = TimeSpan.FromSeconds(10.3),
                SentenceStart = TimeSpan.FromSeconds(9),
                SentenceEnd = TimeSpan.FromSeconds(12)
            }
        };

        // Act
        var ranges = _generator.GenerateMuteRanges(matches, paddingMs: 100, muteEntireSentence: true);

        // Assert
        Assert.Single(ranges);
        Assert.Equal(9000, ranges[0].StartMs);
        Assert.Equal(12000, ranges[0].EndMs);
    }

    [Fact]
    public void GenerateMuteRanges_MergesOverlappingRanges()
    {
        // Arrange
        var matches = new List<ProfanityMatch>
        {
            new ProfanityMatch
            {
                Word = "fuck",
                StartTime = TimeSpan.FromSeconds(10),
                EndTime = TimeSpan.FromSeconds(10.3),
                SentenceStart = TimeSpan.FromSeconds(9),
                SentenceEnd = TimeSpan.FromSeconds(12)
            },
            new ProfanityMatch
            {
                Word = "shit",
                StartTime = TimeSpan.FromSeconds(10.5),
                EndTime = TimeSpan.FromSeconds(10.8),
                SentenceStart = TimeSpan.FromSeconds(9),
                SentenceEnd = TimeSpan.FromSeconds(12)
            }
        };

        // Act
        var ranges = _generator.GenerateMuteRanges(matches, paddingMs: 200, muteEntireSentence: false);

        // Assert
        // With 200ms padding, these should overlap and merge
        Assert.Single(ranges);
        Assert.Contains("fuck", ranges[0].Word);
        Assert.Contains("shit", ranges[0].Word);
    }

    [Fact]
    public void GenerateMuteRanges_KeepsSeparateRanges_WhenNoOverlap()
    {
        // Arrange
        var matches = new List<ProfanityMatch>
        {
            new ProfanityMatch
            {
                Word = "fuck",
                StartTime = TimeSpan.FromSeconds(10),
                EndTime = TimeSpan.FromSeconds(10.3),
                SentenceStart = TimeSpan.FromSeconds(9),
                SentenceEnd = TimeSpan.FromSeconds(12)
            },
            new ProfanityMatch
            {
                Word = "shit",
                StartTime = TimeSpan.FromSeconds(20),
                EndTime = TimeSpan.FromSeconds(20.3),
                SentenceStart = TimeSpan.FromSeconds(19),
                SentenceEnd = TimeSpan.FromSeconds(22)
            }
        };

        // Act
        var ranges = _generator.GenerateMuteRanges(matches, paddingMs: 100, muteEntireSentence: false);

        // Assert
        Assert.Equal(2, ranges.Count);
        Assert.Equal("fuck", ranges[0].Word);
        Assert.Equal("shit", ranges[1].Word);
    }

    [Fact]
    public void GenerateMetadataJson_CreatesValidJson()
    {
        // Arrange
        var ranges = new List<MuteRange>
        {
            new MuteRange { StartMs = 10000, EndMs = 10500, Word = "fuck" },
            new MuteRange { StartMs = 20000, EndMs = 20500, Word = "shit" }
        };

        // Act
        var json = _generator.GenerateMetadataJson(ranges);

        // Assert
        Assert.Contains("\"version\"", json);
        Assert.Contains("\"muteRanges\"", json);
        Assert.Contains("10000", json);
        Assert.Contains("10500", json);
        Assert.Contains("fuck", json);
        Assert.Contains("shit", json);
    }

    [Fact]
    public void GenerateMuteRanges_RespectsSentenceBounds_NoPaddingBeyond()
    {
        // Arrange
        var matches = new List<ProfanityMatch>
        {
            new ProfanityMatch
            {
                Word = "fuck",
                StartTime = TimeSpan.FromSeconds(10),
                EndTime = TimeSpan.FromSeconds(10.05),
                SentenceStart = TimeSpan.FromSeconds(10),
                SentenceEnd = TimeSpan.FromSeconds(10.1)
            }
        };

        // Act
        var ranges = _generator.GenerateMuteRanges(matches, paddingMs: 1000, muteEntireSentence: false);

        // Assert
        Assert.Single(ranges);
        // Should not go before sentence start
        Assert.Equal(10000, ranges[0].StartMs);
        // Should not go after sentence end
        Assert.Equal(100, ranges[0].EndMs);
    }
}
