using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Jellyfin.Plugin.Reelbase.Tests;

/// <summary>
/// The allowance is whatever the API says it is: there is no built-in daily figure. It is read
/// once per run and counted locally from there, and the plugin never spends the last 5%.
/// </summary>
public class RateLimiterTests
{
    [Fact]
    public async Task BeforeTheApiSaysAnything_RequestsAreAllowed()
    {
        var limiter = CreateLimiter();

        Assert.True(await limiter.TryAcquireAsync(default));
        Assert.Null(limiter.Remaining);
        Assert.True(limiter.HasAllowance);
    }

    [Fact]
    public void HeadersAreRead()
    {
        var limiter = CreateLimiter();

        limiter.Observe(Response(("X-RateLimit-Limit", "250"), ("X-RateLimit-Remaining", "42"), ("X-RateLimit-Reset", Reset(TimeSpan.FromHours(1)))));

        Assert.Equal(42, limiter.Remaining);
        Assert.True(limiter.HasAllowance);
    }

    [Fact]
    public void TheAllowanceIsReadOncePerRun_NotOnEveryResponse()
    {
        var limiter = CreateLimiter();
        limiter.Observe(Response(("X-RateLimit-Limit", "1000"), ("X-RateLimit-Remaining", "500"), ("X-RateLimit-Reset", Reset(TimeSpan.FromHours(1)))));

        // A second response from the same run must not overwrite the count.
        limiter.Observe(Response(("X-RateLimit-Limit", "1000"), ("X-RateLimit-Remaining", "480"), ("X-RateLimit-Reset", Reset(TimeSpan.FromHours(1)))));
        Assert.Equal(500, limiter.Remaining);

        // The next run takes a fresh report.
        limiter.BeginRun();
        limiter.Observe(Response(("X-RateLimit-Limit", "1000"), ("X-RateLimit-Remaining", "480"), ("X-RateLimit-Reset", Reset(TimeSpan.FromHours(1)))));
        Assert.Equal(480, limiter.Remaining);
    }

    [Fact]
    public async Task ThePluginCountsItsOwnRequests()
    {
        var limiter = CreateLimiter();
        limiter.Observe(Response(("X-RateLimit-Limit", "1000"), ("X-RateLimit-Remaining", "1000"), ("X-RateLimit-Reset", Reset(TimeSpan.FromHours(1)))));

        Assert.True(await limiter.TryAcquireAsync(default));
        Assert.True(await limiter.TryAcquireAsync(default));
        Assert.True(await limiter.TryAcquireAsync(default));

        Assert.Equal(997, limiter.Remaining);
    }

    [Fact]
    public async Task AnExhaustedAllowance_StopsRequests()
    {
        var limiter = CreateLimiter();

        limiter.Observe(Response(("X-RateLimit-Limit", "1000"), ("X-RateLimit-Remaining", "0"), ("X-RateLimit-Reset", Reset(TimeSpan.FromHours(1)))));

        Assert.False(limiter.HasAllowance);
        Assert.False(await limiter.TryAcquireAsync(default));
    }

    [Fact]
    public async Task OnlyNinetyFivePercentOfTheAllowanceIsSpent()
    {
        var limiter = CreateLimiter();
        limiter.Observe(Response(("X-RateLimit-Limit", "1000"), ("X-RateLimit-Remaining", "1000"), ("X-RateLimit-Reset", Reset(TimeSpan.FromHours(1)))));

        Assert.Equal(50, limiter.Reserved);

        var spent = 0;
        while (await limiter.TryAcquireAsync(default))
        {
            spent++;
        }

        Assert.Equal(950, spent);
        Assert.Equal(50, limiter.Remaining);
    }

    [Fact]
    public async Task TheReserveIsLeftAloneEvenWhenTheAccountIsAlreadyPartlyUsed()
    {
        var limiter = CreateLimiter();
        limiter.Observe(Response(("X-RateLimit-Limit", "1000"), ("X-RateLimit-Remaining", "120"), ("X-RateLimit-Reset", Reset(TimeSpan.FromHours(1)))));

        var spent = 0;
        while (await limiter.TryAcquireAsync(default))
        {
            spent++;
        }

        Assert.Equal(70, spent);
        Assert.Equal(50, limiter.Remaining);
    }

    [Fact]
    public void OnceTheReportedWindowHasPassed_TheAllowanceIsBack()
    {
        var limiter = CreateLimiter();
        limiter.Observe(Response(("X-RateLimit-Limit", "50"), ("X-RateLimit-Remaining", "0"), ("X-RateLimit-Reset", Reset(TimeSpan.FromHours(-1)))));

        Assert.True(limiter.HasAllowance);
        Assert.Equal(50, limiter.Remaining);
    }

    [Fact]
    public void AResponseWithoutAllowanceHeaders_SaysNothing()
    {
        var limiter = CreateLimiter();

        limiter.Observe(Response());

        Assert.Null(limiter.Remaining);
        Assert.True(limiter.HasAllowance);
    }

    [Fact]
    public async Task ALongRetryAfter_StopsTheRequestsInsteadOfWaiting()
    {
        var limiter = CreateLimiter();

        var delay = limiter.ObserveRateLimited(Response(("Retry-After", "3600")));

        Assert.Equal(TimeSpan.FromHours(1), delay);
        Assert.NotNull(limiter.PausedUntil);

        // An hour is longer than a request is willing to wait, so it gives up rather than park.
        Assert.False(await limiter.TryAcquireAsync(default));
    }

    [Fact]
    public async Task AShortRetryAfter_IsWaitedOut()
    {
        var limiter = CreateLimiter();
        limiter.ObserveRateLimited(Response(("Retry-After", "1")));

        var started = DateTimeOffset.UtcNow;

        Assert.True(await limiter.TryAcquireAsync(default));
        Assert.True(DateTimeOffset.UtcNow - started >= TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void RetryAfterMayBeADateInsteadOfSeconds()
    {
        var limiter = CreateLimiter();
        var at = DateTimeOffset.UtcNow.AddMinutes(5);
        using var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        response.Headers.RetryAfter = new RetryConditionHeaderValue(at);

        limiter.ObserveRateLimited(response);

        Assert.InRange(limiter.PausedUntil!.Value, at.AddSeconds(-2), at.AddSeconds(2));
    }

    [Fact]
    public void WithoutRetryAfter_TheResetIsUsed()
    {
        var limiter = CreateLimiter();

        limiter.ObserveRateLimited(Response(("X-RateLimit-Reset", Reset(TimeSpan.FromMinutes(10)))));

        Assert.InRange(
            limiter.PausedUntil!.Value,
            DateTimeOffset.UtcNow.AddMinutes(9),
            DateTimeOffset.UtcNow.AddMinutes(11));
    }

    [Fact]
    public void ARateLimitThatSaysNothingAtAll_FallsBackToAShortPause()
    {
        var limiter = CreateLimiter();

        Assert.Equal(TimeSpan.FromSeconds(60), limiter.ObserveRateLimited(Response()));
    }

    [Fact]
    public void ThePauseSurvivesARestart()
    {
        var store = new MemoryStore();
        var before = new ReelbaseRateLimiter(NullLogger<ReelbaseRateLimiter>.Instance, store, TimeSpan.Zero);
        before.ObserveRateLimited(Response(("Retry-After", "1800")));

        var after = new ReelbaseRateLimiter(NullLogger<ReelbaseRateLimiter>.Instance, store, TimeSpan.Zero);

        Assert.NotNull(after.PausedUntil);
        Assert.False(after.TryAcquireAsync(default).GetAwaiter().GetResult());
    }

    [Fact]
    public void ANwWindowClearsThePause()
    {
        var limiter = CreateLimiter();

        limiter.ObserveRateLimited(Response(("X-RateLimit-Reset", Reset(TimeSpan.FromHours(-1)))));

        // The window the pause belonged to has passed, so the pause goes with it.
        Assert.Null(limiter.PausedUntil);
        Assert.True(limiter.HasAllowance);
    }

    [Fact]
    public void ARestartDoesNotBelieveTheAllowanceIsUntouched()
    {
        var store = new MemoryStore();
        var before = new ReelbaseRateLimiter(NullLogger<ReelbaseRateLimiter>.Instance, store, TimeSpan.Zero);
        before.Observe(Response(("X-RateLimit-Limit", "1000"), ("X-RateLimit-Remaining", "40"), ("X-RateLimit-Reset", Reset(TimeSpan.FromHours(2)))));

        var after = new ReelbaseRateLimiter(NullLogger<ReelbaseRateLimiter>.Instance, store, TimeSpan.Zero);

        // The stored report is used until a response arrives, and it says the account is nearly out.
        Assert.Equal(40, after.Remaining);
        Assert.Equal(50, after.Reserved);
        Assert.False(after.HasAllowance);

        // The first response afterwards replaces it rather than being counted from.
        after.Observe(Response(("X-RateLimit-Limit", "1000"), ("X-RateLimit-Remaining", "900"), ("X-RateLimit-Reset", Reset(TimeSpan.FromHours(2)))));
        Assert.Equal(900, after.Remaining);
        Assert.True(after.HasAllowance);
    }

    private static ReelbaseRateLimiter CreateLimiter()
        => new(NullLogger<ReelbaseRateLimiter>.Instance, new MemoryStore(), TimeSpan.Zero);

    private static string Reset(TimeSpan fromNow)
        => new DateTimeOffset(DateTime.UtcNow.Add(fromNow)).ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static HttpResponseMessage Response(params (string Name, string Value)[] headers)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK);
        foreach (var (name, value) in headers)
        {
            response.Headers.TryAddWithoutValidation(name, value);
        }

        return response;
    }

    private sealed class MemoryStore : IReelbaseAllowanceStore
    {
        private ReelbaseAllowance? _allowance;

        public ReelbaseAllowance? Load() => _allowance;

        public void Save(ReelbaseAllowance allowance) => _allowance = allowance;
    }
}
