using System;
using System.IO;
using System.Text.Json;
using MediaBrowser.Common.Configuration;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Reelbase;

/// <summary>
/// Keeps the allowance in the plugin's configuration folder.
/// </summary>
public sealed class FileReelbaseAllowanceStore : IReelbaseAllowanceStore
{
    private const string FileName = "reelbase-allowance.json";

    private readonly ILogger<FileReelbaseAllowanceStore> _logger;
    private readonly string _path;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileReelbaseAllowanceStore"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="applicationPaths">The server's paths.</param>
    public FileReelbaseAllowanceStore(ILogger<FileReelbaseAllowanceStore> logger, IApplicationPaths applicationPaths)
    {
        _logger = logger;
        _path = Path.Combine(applicationPaths.PluginConfigurationsPath, FileName);
    }

    /// <inheritdoc />
    public ReelbaseAllowance? Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return null;
            }

            var allowance = JsonSerializer.Deserialize<ReelbaseAllowance>(File.ReadAllText(_path));
            if (allowance is null)
            {
                return null;
            }

            // A stored window that has already rolled over says nothing about the next one.
            if (allowance.ResetsAt is { } resetsAt && resetsAt <= DateTimeOffset.UtcNow)
            {
                return null;
            }

            // A pause that has already run out is not worth carrying.
            if (allowance.PausedUntil is { } paused && paused <= DateTimeOffset.UtcNow)
            {
                return allowance with { PausedUntil = null };
            }

            return allowance;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            _logger.LogDebug(ex, "Could not read the stored Reelbase allowance.");
            return null;
        }
    }

    /// <inheritdoc />
    public void Save(ReelbaseAllowance allowance)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(allowance));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogDebug(ex, "Could not store the Reelbase allowance.");
        }
    }
}
