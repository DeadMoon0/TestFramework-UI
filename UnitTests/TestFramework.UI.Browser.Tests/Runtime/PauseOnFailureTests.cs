using TestFramework.UI.Browser.Runtime;
using Xunit;

namespace TestFramework.UI.Browser.Tests.Runtime;

/// <summary>
/// When pause-on-failure holds the browser open, and when it cannot.
/// </summary>
/// <remarks>
/// A headless browser has no window: Playwright's pause returns at once there, so the run used to say the browser was
/// being held open while it held nothing.
/// </remarks>
public sealed class PauseOnFailureTests
{
    [Fact]
    public void NotAskedFor_HoldsNothing()
        => Assert.Equal(nameof(UiHoldPlan.Nothing), UiFailureObserver.PlanHold(false, [false]).ToString());

    [Fact]
    public void AskedForWithNothingOpen_HoldsNothing()
        => Assert.Equal(nameof(UiHoldPlan.Nothing), UiFailureObserver.PlanHold(true, []).ToString());

    [Fact]
    public void AskedForWithOnlyHeadlessBrowsers_SaysItCannotHold()
        => Assert.Equal(nameof(UiHoldPlan.HeadlessOnly), UiFailureObserver.PlanHold(true, [true, true]).ToString());

    [Fact]
    public void AskedForWithAHeadedBrowser_Holds()
        => Assert.Equal(nameof(UiHoldPlan.Hold), UiFailureObserver.PlanHold(true, [true, false]).ToString());
}
