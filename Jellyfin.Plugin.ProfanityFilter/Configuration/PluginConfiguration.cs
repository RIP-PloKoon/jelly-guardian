using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.ProfanityFilter.Configuration;

/// <summary>
/// Plugin configuration.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Gets or sets a value indicating whether the filter is enabled by default for new users.
    /// </summary>
    public bool EnabledByDefault { get; set; } = false;

    /// <summary>
    /// Gets or sets the profanity word list (comma-separated).
    /// Leave empty to use the comprehensive built-in CleanVid word list (751 words).
    /// </summary>
    public string ProfanityWords { get; set; } = "";

    /// <summary>
    /// Gets or sets a value indicating whether to enable word replacement.
    /// When enabled, profanity will be replaced with cleaner alternatives (e.g., "shit" -> "poop", "damn" -> "dang").
    /// </summary>
    public bool EnableWordReplacement { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether to use grammatically-aware replacement.
    /// When enabled, replacements match the grammatical class (noun/verb/adjective/adverb).
    /// Example: "that's shitty" -> "that's crummy" (adjective), "what shit" -> "what nonsense" (noun)
    /// </summary>
    public bool UseGrammaticalReplacement { get; set; } = false;

    /// <summary>
    /// Gets or sets the mute duration padding in milliseconds (before and after detected word).
    /// </summary>
    public int MutePaddingMs { get; set; } = 100;

    /// <summary>
    /// Gets or sets a value indicating whether to mute the entire sentence or just the word.
    /// </summary>
    public bool MuteEntireSentence { get; set; } = false;
}
