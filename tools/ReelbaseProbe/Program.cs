using System.Globalization;
using Jellyfin.Plugin.Reelbase;
using Jellyfin.Plugin.Reelbase.Providers;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// End to end probe: drives the real providers against a real Reelbase instance.
// Usage: REELBASE_API_KEY=rb_... dotnet run --project tools/ReelbaseProbe
var apiKey = Environment.GetEnvironmentVariable("REELBASE_API_KEY");
if (string.IsNullOrWhiteSpace(apiKey))
{
    Console.Error.WriteLine("Set REELBASE_API_KEY first.");
    return 1;
}

var serverUrl = Environment.GetEnvironmentVariable("REELBASE_SERVER_URL") ?? "https://api.reelbase.me";

var services = new ServiceCollection();
services.AddLogging(builder => builder.AddSimpleConsole(options => options.SingleLine = true).SetMinimumLevel(LogLevel.Warning));
services.AddHttpClient();
services.AddMemoryCache();
services.AddSingleton<IReelbaseSettingsProvider>(new StaticSettingsProvider(new ReelbaseSettings
{
    ApiKey = apiKey,
    ServerUrl = serverUrl,
    CacheDurationHours = 24
}));
services.AddSingleton<IReelbaseAllowanceStore, InMemoryAllowanceStore>();
services.AddSingleton<IReelbaseRateLimiter, ReelbaseRateLimiter>();
services.AddScoped<IReelbaseClient, ReelbaseClient>();

using var provider = services.BuildServiceProvider();
using var scope = provider.CreateScope();
var client = scope.ServiceProvider.GetRequiredService<IReelbaseClient>();
var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
var httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();

var limiter = provider.GetRequiredService<IReelbaseRateLimiter>();
var videoProvider = new ReelbaseMusicVideoProvider(httpClientFactory, loggerFactory.CreateLogger<ReelbaseMusicVideoProvider>(), client);
var artistProvider = new ReelbaseMusicArtistProvider(httpClientFactory, loggerFactory.CreateLogger<ReelbaseMusicArtistProvider>(), client);
var personProvider = new ReelbasePersonProvider(httpClientFactory, loggerFactory.CreateLogger<ReelbasePersonProvider>(), client);

Console.WriteLine("=== music video search: Miss Misunderstood / Zolita ===");
var searchInfo = new MusicVideoInfo { Name = "Miss Misunderstood", Artists = new[] { "Zolita" } };
foreach (var hit in await videoProvider.GetSearchResults(searchInfo, CancellationToken.None))
{
    Console.WriteLine($"  {hit.Name} | provider={hit.SearchProviderName} | artist={hit.AlbumArtist?.Name ?? hit.Artists?.FirstOrDefault()?.Name ?? "-"} | {hit.ProductionYear} -> {hit.ProviderIds.GetValueOrDefault("Reelbase")} img={hit.ImageUrl}");
}

Console.WriteLine();
Console.WriteLine("=== music video search: aaliyah_-_try_again (the file name Jellyfin passes on) ===");
foreach (var hit in await videoProvider.GetSearchResults(new MusicVideoInfo { Name = "aaliyah_-_try_again" }, CancellationToken.None))
{
    Console.WriteLine($"  {hit.Name} | artist={hit.AlbumArtist?.Name ?? hit.Artists?.FirstOrDefault()?.Name ?? "-"} | {hit.ProductionYear} -> {hit.ProviderIds.GetValueOrDefault("Reelbase")}");
}

Console.WriteLine();
Console.WriteLine("=== music video metadata: aaliyah_-_try_again ===");
PrintVideo(await videoProvider.GetMetadata(
    new MusicVideoInfo { Name = "aaliyah_-_try_again" },
    CancellationToken.None));

Console.WriteLine();
Console.WriteLine("=== music video metadata: Zolita - Miss Misunderstood ===");
PrintVideo(await videoProvider.GetMetadata(
    new MusicVideoInfo { Name = "Zolita - Miss Misunderstood", Artists = new[] { "Zolita" } },
    CancellationToken.None));

Console.WriteLine();
Console.WriteLine("=== music video metadata: Zwonaka - Suntanning (for Blacks) ===");
var second = await videoProvider.GetMetadata(
    new MusicVideoInfo { Name = "Zwonaka - Suntanning (for Blacks)", Artists = new[] { "Zwonaka" } },
    CancellationToken.None);
PrintVideo(second);

Console.WriteLine();
Console.WriteLine("=== artist metadata: Zolita ===");
var artistResult = await artistProvider.GetMetadata(new ArtistInfo { Name = "Zolita" }, CancellationToken.None);
Console.WriteLine($"  HasMetadata={artistResult.HasMetadata} Name={artistResult.Item?.Name} Year={artistResult.Item?.ProductionYear} Url={artistResult.Item?.HomePageUrl} Id={artistResult.Item?.ProviderIds.GetValueOrDefault("Reelbase")}");
Console.WriteLine($"  Overview={Trim(artistResult.Item?.Overview, 100)}");

Console.WriteLine();
Console.WriteLine("=== person metadata: Annette Milburn ===");
var personResult = await personProvider.GetMetadata(new PersonLookupInfo { Name = "Annette Milburn" }, CancellationToken.None);
Console.WriteLine($"  HasMetadata={personResult.HasMetadata} Name={personResult.Item?.Name} Id={personResult.Item?.ProviderIds.GetValueOrDefault("Reelbase")}");
Console.WriteLine($"  Overview={Trim(personResult.Item?.Overview, 100)}");

Console.WriteLine();
Console.WriteLine("=== image provider ===");
var imageProvider = new ReelbaseImageProvider(httpClientFactory, client);
if (second.Item is { } videoItem)
{
    foreach (var image in await imageProvider.GetImages(videoItem, CancellationToken.None))
    {
        Console.WriteLine($"  {image.Type} {image.ProviderName} {image.Url}");
    }
}

Console.WriteLine();
Console.WriteLine("=== allowance, as reported by the API ===");
Console.WriteLine($"  remaining={limiter.Remaining?.ToString(CultureInfo.InvariantCulture) ?? "(not reported)"} reserved={limiter.Reserved} resets={limiter.ResetsAt?.ToString("u", CultureInfo.InvariantCulture) ?? "-"} paused={limiter.PausedUntil?.ToString("u", CultureInfo.InvariantCulture) ?? "-"} hasAllowance={limiter.HasAllowance}");

return 0;

static string Trim(string? value, int length)
    => string.IsNullOrEmpty(value) ? "-" : (value.Length > length ? value[..length] + "..." : value);

static void PrintVideo(MetadataResult<MusicVideo> result)
{
    if (!result.HasMetadata || result.Item is null)
    {
        Console.WriteLine("  (no metadata)");
        return;
    }

    var item = result.Item;
    Console.WriteLine($"  Name: {item.Name}");
    Console.WriteLine($"  Year: {item.ProductionYear}  Released: {item.PremiereDate:yyyy-MM-dd}  Runtime: {(item.RunTimeTicks is { } t ? TimeSpan.FromTicks(t).ToString("c", CultureInfo.InvariantCulture) : "-")}");
    Console.WriteLine($"  Artists: {string.Join(", ", item.Artists ?? Array.Empty<string>())}");
    Console.WriteLine($"  Album: {(string.IsNullOrWhiteSpace(item.Album) ? "-" : item.Album)}");
    Console.WriteLine($"  Genres: {string.Join(", ", item.Genres ?? Array.Empty<string>())}");
    Console.WriteLine($"  Studios: {string.Join(", ", item.Studios ?? Array.Empty<string>())}");
    Console.WriteLine($"  Overview: {Trim(item.Overview, 100)}");
    Console.WriteLine($"  ProviderIds: {string.Join(", ", item.ProviderIds.Select(kv => kv.Key + "=" + kv.Value))}");
    Console.WriteLine($"  Images: {string.Join(", ", item.ImageInfos.Select(i => i.Path))}");
    Console.WriteLine($"  People ({result.People.Count}):");
    foreach (var person in result.People.Take(8))
    {
        Console.WriteLine($"    - {person.Name} [{person.Type}] {person.Role} {person.ProviderIds.GetValueOrDefault("Reelbase")}");
    }
}

internal sealed class StaticSettingsProvider : IReelbaseSettingsProvider
{
    public StaticSettingsProvider(ReelbaseSettings settings) => Current = settings;

    public ReelbaseSettings Current { get; }
}
internal sealed class InMemoryAllowanceStore : IReelbaseAllowanceStore
{
    private ReelbaseAllowance? _allowance;

    public ReelbaseAllowance? Load() => _allowance;

    public void Save(ReelbaseAllowance allowance) => _allowance = allowance;
}
