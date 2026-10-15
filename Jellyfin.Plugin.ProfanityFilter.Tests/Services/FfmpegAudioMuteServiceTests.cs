using System.Collections.Generic;
using Jellyfin.Plugin.ProfanityFilter.Services;
using Xunit;

namespace Jellyfin.Plugin.ProfanityFilter.Tests.Services;

public class FfmpegAudioMuteServiceTests
{
    [Fact]
    public void GetOutputPath_UsesDiscoverableSidecarAudioName()
    {
        var outputPath = FfmpegAudioMuteService.GetOutputPath("/media/Example Movie (2026).mkv");

        Assert.Equal("/media/Example Movie (2026).profanity-filter.eng.m4a", outputPath);
    }

    [Fact]
    public void BuildMuteFilter_SortsRangesAndUsesInvariantSeconds()
    {
        var filter = FfmpegAudioMuteService.BuildMuteFilter(new List<MuteRange>
        {
            new() { StartMs = 12345, EndMs = 12789 },
            new() { StartMs = 500, EndMs = 1000 }
        });

        Assert.Equal(
            "volume=volume=0:enable='between(t,0.5,1)',volume=volume=0:enable='between(t,12.345,12.789)'",
            filter);
    }
}
