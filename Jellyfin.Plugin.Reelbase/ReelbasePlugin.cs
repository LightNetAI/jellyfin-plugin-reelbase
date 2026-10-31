using System;
using System.Collections.Generic;
using Jellyfin.Plugin.Reelbase.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.Reelbase;

/// <summary>
/// The Reelbase plugin.
/// </summary>
public class ReelbasePlugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    /// <summary>
    /// The name the plugin registers its metadata providers under, and the key its provider ids
    /// are stored under.
    /// </summary>
    public const string ProviderName = "Reelbase";

    /// <summary>
    /// The instance the plugin is configured against until a server url is entered.
    /// </summary>
    public const string DefaultServerUrl = "https://api.reelbase.me";

    /// <summary>
    /// Initializes a new instance of the <see cref="ReelbasePlugin"/> class.
    /// </summary>
    /// <param name="applicationPaths">Instance of the <see cref="IApplicationPaths"/> interface.</param>
    /// <param name="xmlSerializer">Instance of the <see cref="IXmlSerializer"/> interface.</param>
    public ReelbasePlugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    /// <summary>
    /// Gets the current plugin instance.
    /// </summary>
    public static ReelbasePlugin? Instance { get; private set; }

    /// <inheritdoc />
    public override string Name => ProviderName;

    /// <inheritdoc />
    public override Guid Id => new Guid("816fb33f-67c2-485f-8f47-8c7877ea6e70");

    /// <inheritdoc />
    public override string Description => "Get music video metadata and artwork from Reelbase.";

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages()
    {
        yield return new PluginPageInfo
        {
            Name = Name,
            EmbeddedResourcePath = $"{GetType().Namespace}.Configuration.config.html"
        };
    }
}
