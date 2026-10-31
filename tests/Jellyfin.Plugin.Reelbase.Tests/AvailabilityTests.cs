using System;
using Xunit;

namespace Jellyfin.Plugin.Reelbase.Tests;

/// <summary>
/// A library refresh can visit thousands of items, so an API that is failing must not be asked
/// once per item.
/// </summary>
public class AvailabilityTests
{
    public AvailabilityTests()
    {
        ReelbaseAvailability.Reset();
    }

    [Fact]
    public void AFewFailuresDoNotStopAnything()
    {
        for (var i = 0; i < ReelbaseAvailability.FailureThreshold - 1; i++)
        {
            Assert.False(ReelbaseAvailability.RecordFailure());
        }

        Assert.True(ReelbaseAvailability.IsAvailable);
    }

    [Fact]
    public void EnoughFailuresInARow_StopTheRequests()
    {
        var opened = false;

        for (var i = 0; i < ReelbaseAvailability.FailureThreshold; i++)
        {
            opened |= ReelbaseAvailability.RecordFailure();
        }

        Assert.True(opened);
        Assert.False(ReelbaseAvailability.IsAvailable);
    }

    [Fact]
    public void ASuccessClosesItAgain()
    {
        for (var i = 0; i < ReelbaseAvailability.FailureThreshold; i++)
        {
            ReelbaseAvailability.RecordFailure();
        }

        ReelbaseAvailability.RecordSuccess();

        Assert.True(ReelbaseAvailability.IsAvailable);
    }

    [Fact]
    public void ItTriesAgainOnceTheCoolDownHasPassed()
    {
        ReelbaseAvailability.CoolDown = TimeSpan.Zero;

        for (var i = 0; i < ReelbaseAvailability.FailureThreshold; i++)
        {
            ReelbaseAvailability.RecordFailure();
        }

        Assert.True(ReelbaseAvailability.IsAvailable);
    }

    [Fact]
    public void OpeningItIsOnlyWorthOneLogLine()
    {
        var opened = 0;

        for (var i = 0; i < ReelbaseAvailability.FailureThreshold * 3; i++)
        {
            if (ReelbaseAvailability.RecordFailure())
            {
                opened++;
            }
        }

        Assert.Equal(1, opened);
    }
}
