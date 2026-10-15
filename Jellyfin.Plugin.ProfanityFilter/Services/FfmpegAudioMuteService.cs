using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ProfanityFilter.Services;

/// <summary>
/// Creates an external muted audio track from the first audio stream in a media file.
/// </summary>
public sealed class FfmpegAudioMuteService
{
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FfmpegAudioMuteService"/> class.
    /// </summary>
    /// <param name="logger">Logger for FFmpeg execution results.</param>
    public FfmpegAudioMuteService(ILogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Creates a muted AAC sidecar track next to the supplied media file.
    /// </summary>
    /// <param name="mediaPath">Source media path.</param>
    /// <param name="muteRanges">Intervals to mute.</param>
    /// <param name="ffmpegPath">FFmpeg executable path or command name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true"/> when the sidecar track was created.</returns>
    public async Task<bool> CreateMutedAudioTrackAsync(
        string mediaPath,
        IReadOnlyCollection<MuteRange> muteRanges,
        string ffmpegPath,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(mediaPath) || !File.Exists(mediaPath))
        {
            _logger.LogError("Cannot create a muted audio track because the media file does not exist: {MediaPath}", mediaPath);
            return false;
        }

        if (muteRanges.Count == 0)
        {
            return false;
        }

        var outputPath = GetOutputPath(mediaPath);
        var temporaryPath = outputPath + "." + Guid.NewGuid().ToString("N") + ".tmp.m4a";

        try
        {
            var startInfo = CreateStartInfo(mediaPath, temporaryPath, muteRanges, ffmpegPath);
            using var process = new Process { StartInfo = startInfo };

            if (!process.Start())
            {
                _logger.LogError("FFmpeg did not start while creating a muted audio track for {MediaPath}", mediaPath);
                return false;
            }

            var standardError = process.StandardError.ReadToEndAsync(cancellationToken);
            try
            {
                await process.WaitForExitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                process.Kill(entireProcessTree: true);
                throw;
            }

            var errorOutput = await standardError;
            if (process.ExitCode != 0)
            {
                _logger.LogError(
                    "FFmpeg exited with code {ExitCode} while creating a muted audio track for {MediaPath}: {ErrorOutput}",
                    process.ExitCode,
                    mediaPath,
                    errorOutput);
                return false;
            }

            File.Move(temporaryPath, outputPath, overwrite: true);
            _logger.LogInformation("Created muted audio track for {MediaPath}: {OutputPath}", mediaPath, outputPath);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            _logger.LogError(ex, "Unable to create a muted audio track for {MediaPath}", mediaPath);
            return false;
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    /// <summary>
    /// Gets the sidecar-audio output path for a media file.
    /// </summary>
    /// <param name="mediaPath">Source media path.</param>
    /// <returns>Muted AAC sidecar path.</returns>
    public static string GetOutputPath(string mediaPath)
    {
        return Path.Combine(
            Path.GetDirectoryName(mediaPath) ?? string.Empty,
            Path.GetFileNameWithoutExtension(mediaPath) + ".profanity-filter.eng.m4a");
    }

    /// <summary>
    /// Builds the FFmpeg audio-filter expression for mute ranges.
    /// </summary>
    /// <param name="muteRanges">Intervals to mute.</param>
    /// <returns>FFmpeg filter expression.</returns>
    public static string BuildMuteFilter(IReadOnlyCollection<MuteRange> muteRanges)
    {
        return string.Join(
            ",",
            muteRanges
                .OrderBy(range => range.StartMs)
                .Select(range =>
                    $"volume=volume=0:enable='between(t,{ToSeconds(range.StartMs)},{ToSeconds(range.EndMs)})'"));
    }

    private static ProcessStartInfo CreateStartInfo(
        string mediaPath,
        string outputPath,
        IReadOnlyCollection<MuteRange> muteRanges,
        string ffmpegPath)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = false,
            CreateNoWindow = true
        };

        startInfo.ArgumentList.Add("-y");
        startInfo.ArgumentList.Add("-nostdin");
        startInfo.ArgumentList.Add("-hide_banner");
        startInfo.ArgumentList.Add("-loglevel");
        startInfo.ArgumentList.Add("error");
        startInfo.ArgumentList.Add("-i");
        startInfo.ArgumentList.Add(mediaPath);
        startInfo.ArgumentList.Add("-map");
        startInfo.ArgumentList.Add("0:a:0");
        startInfo.ArgumentList.Add("-vn");
        startInfo.ArgumentList.Add("-af");
        startInfo.ArgumentList.Add(BuildMuteFilter(muteRanges));
        startInfo.ArgumentList.Add("-c:a");
        startInfo.ArgumentList.Add("aac");
        startInfo.ArgumentList.Add("-b:a");
        startInfo.ArgumentList.Add("192k");
        startInfo.ArgumentList.Add("-metadata");
        startInfo.ArgumentList.Add("title=Profanity Filter (Muted)");
        startInfo.ArgumentList.Add("-movflags");
        startInfo.ArgumentList.Add("+faststart");
        startInfo.ArgumentList.Add(outputPath);

        return startInfo;
    }

    private static string ToSeconds(long milliseconds)
    {
        return (milliseconds / 1000d).ToString("0.###", CultureInfo.InvariantCulture);
    }
}
