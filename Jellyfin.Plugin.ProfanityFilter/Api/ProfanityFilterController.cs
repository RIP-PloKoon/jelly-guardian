using System;
using System.IO;
using System.Threading.Tasks;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.ProfanityFilter.Api;

/// <summary>
/// Profanity filter API controller.
/// </summary>
[ApiController]
[Route("ProfanityFilter")]
[Authorize]
public class ProfanityFilterController : ControllerBase
{
    private readonly IServerApplicationPaths _appPaths;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProfanityFilterController"/> class.
    /// </summary>
    /// <param name="appPaths">Instance of the <see cref="IServerApplicationPaths"/> interface.</param>
    public ProfanityFilterController(IServerApplicationPaths appPaths)
    {
        _appPaths = appPaths;
    }

    /// <summary>
    /// Get profanity filter metadata for an item.
    /// </summary>
    /// <param name="itemId">The item ID.</param>
    /// <returns>Profanity filter metadata JSON.</returns>
    [HttpGet("Metadata/{itemId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<string>> GetMetadata([FromRoute] Guid itemId)
    {
        // This is a simplified version - in production you'd use ILibraryManager
        // to get the actual item path
        var metadataPath = $"{itemId}.profanity.json";
        
        if (!System.IO.File.Exists(metadataPath))
        {
            return NotFound("Profanity filter metadata not found for this item");
        }

        var content = await System.IO.File.ReadAllTextAsync(metadataPath);
        return Ok(content);
    }

    /// <summary>
    /// Get user's profanity filter preferences.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <returns>User preferences.</returns>
    [HttpGet("UserPreferences/{userId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<UserPreferences> GetUserPreferences([FromRoute] Guid userId)
    {
        // In production, this would be stored in a database or user settings
        // For now, return default from plugin config
        var config = Plugin.Instance?.Configuration;
        
        return Ok(new UserPreferences
        {
            Enabled = config?.EnabledByDefault ?? false,
            MuteEntireSentence = config?.MuteEntireSentence ?? false
        });
    }

    /// <summary>
    /// Update user's profanity filter preferences.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="preferences">User preferences.</param>
    /// <returns>Updated preferences.</returns>
    [HttpPost("UserPreferences/{userId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<UserPreferences> UpdateUserPreferences(
        [FromRoute] Guid userId,
        [FromBody] UserPreferences preferences)
    {
        // In production, save to database or user settings
        // For now, just return what was sent
        return Ok(preferences);
    }
}

/// <summary>
/// User preferences for profanity filter.
/// </summary>
public class UserPreferences
{
    /// <summary>
    /// Gets or sets a value indicating whether the filter is enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to mute entire sentences.
    /// </summary>
    public bool MuteEntireSentence { get; set; }
}
