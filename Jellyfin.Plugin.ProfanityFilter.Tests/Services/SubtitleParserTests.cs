using Xunit;
using Jellyfin.Plugin.ProfanityFilter.Services;

namespace Jellyfin.Plugin.ProfanityFilter.Tests.Services;

public class SubtitleParserTests
{
    private readonly SubtitleParser _parser;

    public SubtitleParserTests()
    {
        _parser = new SubtitleParser();
    }

    [Fact]
    public void ParseSrt_ValidContent_ReturnsEntries()
    {
        // Arrange
        var srtContent = @"1
00:00:20,000 --> 00:00:24,400
This is the first subtitle

2
00:00:30,000 --> 00:00:35,600
This contains a bad word: fuck

3
00:01:00,000 --> 00:01:05,000
Another clean subtitle here";

        // Act
        var entries = _parser.ParseSrt(srtContent);

        // Assert
        Assert.Equal(3, entries.Count);
        Assert.Equal("This is the first subtitle", entries[0].Text);
        Assert.Contains("fuck", entries[1].Text);
        Assert.Equal(20000, entries[0].StartTime.TotalMilliseconds);
        Assert.Equal(24400, entries[0].EndTime.TotalMilliseconds);
    }

    [Fact]
    public void ParseVtt_ValidContent_ReturnsEntries()
    {
        // Arrange
        var vttContent = @"WEBVTT

00:00:20.000 --> 00:00:24.400
This is the first subtitle

00:00:30.000 --> 00:00:35.600
This contains a bad word: shit

00:01:00.000 --> 00:01:05.000
Another clean subtitle here";

        // Act
        var entries = _parser.ParseVtt(vttContent);

        // Assert
        Assert.Equal(3, entries.Count);
        Assert.Equal("This is the first subtitle", entries[0].Text);
        Assert.Contains("shit", entries[1].Text);
        Assert.Equal(20000, entries[0].StartTime.TotalMilliseconds);
    }

    [Fact]
    public void ParseSrt_MultilineSubtitle_CombinesText()
    {
        // Arrange
        var srtContent = @"1
00:00:20,000 --> 00:00:24,400
Line one
Line two
Line three";

        // Act
        var entries = _parser.ParseSrt(srtContent);

        // Assert
        Assert.Single(entries);
        Assert.Contains("Line one", entries[0].Text);
        Assert.Contains("Line two", entries[0].Text);
        Assert.Contains("Line three", entries[0].Text);
    }

    [Fact]
    public void ParseSrt_EmptyContent_ReturnsEmptyList()
    {
        // Act
        var entries = _parser.ParseSrt("");

        // Assert
        Assert.Empty(entries);
    }
}
