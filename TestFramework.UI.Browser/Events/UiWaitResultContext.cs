using System.Globalization;
using TestFramework.Core.Steps;

namespace TestFramework.UI.Browser.Events;

/// <summary>
/// What a wait waited for, and how long it took.
/// </summary>
/// <param name="App">The application that was watched.</param>
/// <param name="Url">The address the page was on when the wait was over.</param>
/// <param name="Waited">What was waited for, in the words the test used.</param>
/// <param name="ResolvedVia">Which lookup channel answered, when the wait was for an element.</param>
/// <param name="WaitedForMs">How long the wait took.</param>
/// <param name="Polls">How many times the page was asked.</param>
public sealed record UiWaitResultContext(
    string App,
    string Url,
    string Waited,
    string? ResolvedVia,
    double WaitedForMs,
    int Polls) : StepResultContext
{
    /// <summary>
    /// Returns a readable summary.
    /// </summary>
    /// <returns>The summary.</returns>
    public override string ToString()
        => string.Format(
            CultureInfo.InvariantCulture,
            "{0} on '{1}' after {2:F1}s ({3} poll(s))",
            this.Waited,
            this.App,
            this.WaitedForMs / 1000,
            this.Polls);
}
