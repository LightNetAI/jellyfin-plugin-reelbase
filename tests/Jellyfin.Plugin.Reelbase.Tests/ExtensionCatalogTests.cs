using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Reelbase.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Jellyfin.Plugin.Reelbase.Tests;

/// <summary>
/// The approved extension list decides, without a request, whether a file is worth looking up.
/// It is downloaded once, saved beside the plugin configuration, and only downloaded again when
/// the version the API reports has moved on.
/// </summary>
public class ExtensionCatalogTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "reelbase-extensions-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try
        {
            Directory.Delete(_folder, true);
        }
        catch (IOException)
        {
            // A leftover directory in the temp folder is not worth failing a test over.
        }
    }

    [Fact]
    public void AllowsEverythingBeforeAListIsKnown()
    {
        var catalog = CreateCatalog();

        Assert.False(catalog.IsKnown);
        Assert.True(catalog.IsAllowed("mkv"));
    }

    [Fact]
    public void AllowsAFolderOrANameWithNoExtension()
    {
        var catalog = CreateCatalog();

        Assert.True(catalog.IsAllowed(ReelbaseFileExtension.None));
        Assert.True(catalog.IsAllowed(null));
        Assert.True(catalog.IsAllowed("  "));
    }

    [Fact]
    public async Task DownloadsTheListWhenTheVersionChanges()
    {
        var catalog = CreateCatalog();
        var client = new StubClient(4, ["mkv", "mp4"]);

        Assert.True(await catalog.RefreshAsync(client, CancellationToken.None));

        Assert.Equal(4, catalog.Version);
        Assert.Equal(1, client.ListCalls);
        Assert.True(catalog.IsAllowed("mkv"));
        Assert.True(catalog.IsAllowed("MKV"));
        Assert.False(catalog.IsAllowed("avi"));
    }

    [Fact]
    public async Task AsksOnlyForTheVersionWhenItHasNotMoved()
    {
        var catalog = CreateCatalog();
        var client = new StubClient(4, ["mkv"]);
        await catalog.RefreshAsync(client, CancellationToken.None);

        var second = await catalog.RefreshAsync(client, CancellationToken.None);

        Assert.False(second);
        Assert.Equal(1, client.ListCalls);
        Assert.Equal(2, client.VersionCalls);
    }

    [Fact]
    public async Task KeepsTheListOnDiskForTheNextScan()
    {
        await CreateCatalog().RefreshAsync(new StubClient(7, ["mkv"]), CancellationToken.None);

        var reloaded = CreateCatalog();

        Assert.True(reloaded.IsKnown);
        Assert.Equal(7, reloaded.Version);
        Assert.True(reloaded.IsAllowed("mkv"));
        Assert.False(reloaded.IsAllowed("avi"));
        Assert.True(File.Exists(Path.Combine(_folder, "Reelbase", "extensions.json")));
    }

    [Fact]
    public async Task KeepsTheSavedListWhenTheApiDoesNotAnswer()
    {
        var catalog = CreateCatalog();
        await catalog.RefreshAsync(new StubClient(7, ["mkv"]), CancellationToken.None);

        var refreshed = await catalog.RefreshAsync(new StubClient(null, []), CancellationToken.None);

        Assert.False(refreshed);
        Assert.Equal(7, catalog.Version);
        Assert.False(catalog.IsAllowed("avi"));
    }

    private ReelbaseExtensionCatalog CreateCatalog()
        => new(
            Path.Combine(_folder, "Reelbase", "extensions.json"),
            NullLogger<ReelbaseExtensionCatalog>.Instance);

    private sealed class StubClient : IReelbaseClient
    {
        private readonly int? _version;
        private readonly IReadOnlyList<string> _extensions;

        public StubClient(int? version, IReadOnlyList<string> extensions)
        {
            _version = version;
            _extensions = extensions;
        }

        public int VersionCalls { get; private set; }

        public int ListCalls { get; private set; }

        public Task<int?> GetExtensionsVersionAsync(CancellationToken cancellationToken)
        {
            VersionCalls++;
            return Task.FromResult(_version);
        }

        public Task<ReelbaseExtensionListResponse?> GetExtensionsAsync(CancellationToken cancellationToken)
        {
            ListCalls++;
            return Task.FromResult<ReelbaseExtensionListResponse?>(new ReelbaseExtensionListResponse
            {
                Data = _extensions,
                Meta = new ReelbaseExtensionListMeta { Version = _version ?? 0, Count = _extensions.Count }
            });
        }

        public Task<IReadOnlyList<ReelbaseVideo>> SearchVideosAsync(string query, string extension, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<ReelbaseVideo?> GetVideoAsync(string slug, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<ReelbaseArtist>> SearchArtistsAsync(string query, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<ReelbaseArtist?> GetArtistAsync(string slug, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<ReelbasePerson>> SearchPeopleAsync(string query, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<ReelbasePerson?> GetPersonAsync(string slug, CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }
}
