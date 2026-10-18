namespace TestFramework.UI.Browser.Runtime;

/// <summary>
/// What a failure does with the browser when pause-on-failure is asked for.
/// </summary>
internal enum UiHoldPlan
{
    /// <summary>Not asked for, or nothing is open.</summary>
    Nothing,

    /// <summary>Every open browser is headless: there is no window to hold, and the run says so.</summary>
    HeadlessOnly,

    /// <summary>The headed browsers are held open until the person lets the run continue.</summary>
    Hold,
}
