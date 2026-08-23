using Xunit;

namespace TestFramework.UI.Browser.Tests.Shared;

/// <summary>
/// A test that drives a real browser, skipped with its reason when the machine cannot.
/// </summary>
/// <remarks>
/// The skip is decided in the constructor, which is how xunit 2 makes a test skip for a reason computed
/// at run time. In xunit 3 this becomes a dynamic skip and the reason can be produced per test case; the
/// attribute is the only thing that would change.
/// </remarks>
public sealed class BrowserFactAttribute : FactAttribute
{
    /// <summary>
    /// Creates the attribute, skipping the test when no browser was asked for or the sample application
    /// has not been built.
    /// </summary>
    public BrowserFactAttribute()
    {
        if (UiTestEnvironmentGate.SkipReason() is { } reason)
        {
            this.Skip = reason;
        }
    }
}
