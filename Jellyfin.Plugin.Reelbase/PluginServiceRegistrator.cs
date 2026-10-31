using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.Reelbase;

/// <summary>
/// Registers the services the plugin needs.
/// </summary>
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<IReelbaseSettingsProvider, PluginSettingsProvider>();
        serviceCollection.AddSingleton<IReelbaseAllowanceStore, FileReelbaseAllowanceStore>();
        serviceCollection.AddSingleton<IReelbaseRateLimiter, ReelbaseRateLimiter>();
        serviceCollection.AddScoped<IReelbaseClient, ReelbaseClient>();
    }
}
