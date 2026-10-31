using System.Text.Json;
using Jellyfin.Extensions.Json;

namespace Jellyfin.Plugin.Reelbase.Tests;

/// <summary>
/// Loads the API responses captured from a live Reelbase instance, so that the tests fail when
/// the shape of the API stops matching the models.
/// </summary>
internal static class Fixture
{
    public static T Load<T>(string name)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", name);
        var json = File.ReadAllText(path);

        return JsonSerializer.Deserialize<T>(json, JsonDefaults.Options)
            ?? throw new InvalidOperationException("Could not deserialize " + name + ".");
    }
}
