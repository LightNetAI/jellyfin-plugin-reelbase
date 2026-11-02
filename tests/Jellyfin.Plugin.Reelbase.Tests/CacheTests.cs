using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Jellyfin.Plugin.Reelbase.Tests;

/// <summary>
/// A lookup that finds nothing is not remembered. Records are added to Reelbase all the time, and
/// a miss cached for the whole cache window is a lookup that never reaches the API again - which
/// reads as the plugin not sending anything at all.
/// </summary>
public class CacheTests
{
    [Fact]
    public async Task AnEmptyAnswerIsAskedForAgain()
    {
        var handler = new StubHandler("""{"data":[],"meta":{"page":1,"per_page":25,"total":0,"last_page":1}}""");
        var client = CreateClient(handler);

        await client.SearchVideosAsync("Domingo, Netrebko & Villazón", "mkv", CancellationToken.None);
        await client.SearchVideosAsync("Domingo, Netrebko & Villazón", "mkv", CancellationToken.None);

        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task AnAnswerWithARecordIsReused()
    {
        var handler = new StubHandler("""{"data":[{"title":"Jóga","slug":"joga","artist":{"name":"Björk","slug":"bjork"}}]}""");
        var client = CreateClient(handler);

        var first = await client.SearchVideosAsync("Björk", "mkv", CancellationToken.None);
        var second = await client.SearchVideosAsync("Björk", "mkv", CancellationToken.None);

        Assert.Single(first);
        Assert.Single(second);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task TheRequestCarriesTheFileExtension()
    {
        var handler = new StubHandler("""{"data":[]}""");
        var client = CreateClient(handler);

        await client.SearchVideosAsync("Björk - Jóga", "mkv", CancellationToken.None);
        await client.SearchVideosAsync("Linkin Park", ReelbaseFileExtension.None, CancellationToken.None);

        Assert.Contains("ext=mkv", handler.Requests[0], StringComparison.Ordinal);
        Assert.Contains("ext=null", handler.Requests[1], StringComparison.Ordinal);
    }

    private static ReelbaseClient CreateClient(HttpMessageHandler handler)
        => new(
            new StubFactory(handler),
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<ReelbaseClient>.Instance,
            new StubSettings(),
            new ReelbaseRateLimiter(NullLogger<ReelbaseRateLimiter>.Instance, new StubStore(), TimeSpan.Zero));

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly string _body;

        public StubHandler(string body) => _body = body;

        public int Calls { get; private set; }

        public List<string> Requests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            Requests.Add(request.RequestUri!.ToString());
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_body, System.Text.Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class StubFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;

        public StubFactory(HttpMessageHandler handler) => _handler = handler;

        public HttpClient CreateClient(string name) => new(_handler, disposeHandler: false);
    }

    private sealed class StubSettings : IReelbaseSettingsProvider
    {
        public ReelbaseSettings Current { get; } = new() { ApiKey = "test-key", ServerUrl = "https://api.reelbase.me" };
    }

    private sealed class StubStore : IReelbaseAllowanceStore
    {
        private ReelbaseAllowance? _allowance;

        public ReelbaseAllowance? Load() => _allowance;

        public void Save(ReelbaseAllowance allowance) => _allowance = allowance;
    }
}
