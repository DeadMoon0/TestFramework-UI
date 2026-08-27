using System;
using System.Threading;
using TestFramework.Core.Steps;
using TestFramework.UI.Browser.Events;
using Xunit;

namespace TestFramework.UI.Browser.Tests.Events;

/// <summary>
/// The arithmetic that keeps a wait out of Playwright when its own deadline is about to fire.
/// </summary>
/// <remarks>
/// The race this settles is not reproducible on demand - it needs a machine busy enough that a browser call
/// outlives the grace window - so what is pinned here is the part that can be broken silently. A wrong value
/// does not fail; it just restores the old symptom, where the engine abandons the step and prints its generic
/// timeout instead of the wait's own account of what the page read.
/// </remarks>
public class ProbeBudgetTests
{
    [Fact]
    public void AnExpiredDeadlineNeverAsksPlaywrightToWaitForever()
    {
        // The one value that must never be produced. Playwright reads a zero timeout as "no timeout", so an
        // expired budget passed through unclamped would wait the full ten seconds - the exact behaviour this
        // type exists to prevent, and it would arrive silently.
        StepDeadline expired = Deadline(TimeSpan.FromSeconds(3), elapsed: TimeSpan.FromSeconds(5));

        float? milliseconds = ProbeBudget.For(expired).Milliseconds;

        Assert.NotNull(milliseconds);
        Assert.True(milliseconds > 0, $"an expired budget produced {milliseconds}, which Playwright reads as 'wait forever'");
    }

    [Fact]
    public void AStepWithTimeLeftGetsExactlyWhatIsLeft()
    {
        StepDeadline deadline = Deadline(TimeSpan.FromSeconds(3), elapsed: TimeSpan.FromSeconds(1));

        Assert.Equal(2000f, ProbeBudget.For(deadline).Milliseconds);
    }

    [Fact]
    public void AnUnboundedStepLeavesPlaywrightsOwnDefaultAlone()
    {
        // No deadline to protect, so imposing one would shorten waits nobody asked to shorten.
        Assert.Null(ProbeBudget.For(Deadline(Timeout.InfiniteTimeSpan, TimeSpan.Zero)).Milliseconds);
        Assert.Null(ProbeBudget.Unbounded.Milliseconds);
    }

    private static StepDeadline Deadline(TimeSpan total, TimeSpan elapsed)
        => (StepDeadline)Activator.CreateInstance(
            typeof(StepDeadline),
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
            binder: null,
            [total, CancellationToken.None, (Func<TimeSpan>)(() => elapsed)],
            culture: null)!;
}
