using System;
using TestFramework.Core.Steps;

namespace TestFramework.UI.Browser.Events;

/// <summary>
/// How long one poll may spend inside Playwright before the step's own deadline needs the time back.
/// </summary>
/// <remarks>
/// <para>
/// A step that runs out of time gets a short grace window to say what went wrong, and a wait spends that
/// window building its account: what it was waiting for, what the page actually read, where the failure
/// bundle is. It can only do that if it is not still sitting inside a browser call when the deadline fires.
/// </para>
/// <para>
/// Most probe calls answer immediately - counting matches, asking whether an element is visible - and those
/// were never the problem. The ones that auto-wait are: Playwright waits for the element to be attached
/// before reading it, bounded by the page's default timeout, which is the configured action timeout and ten
/// seconds out of the box. A three-second wait therefore had one second of grace against a call that could
/// legitimately take ten, and when it lost that race the engine abandoned the step and printed its generic
/// timeout instead of the wait's own diagnosis. Nothing was wrong with the page; the message simply never
/// got written.
/// </para>
/// <para>
/// So a poll is given exactly what is left of the step. When that runs out Playwright gives up first, the
/// poll reads as "not yet" - which is what it is - and the loop comes back to find the deadline expired
/// with the whole grace window still unspent.
/// </para>
/// </remarks>
internal readonly struct ProbeBudget
{
    private readonly float? milliseconds;

    private ProbeBudget(float? milliseconds) => this.milliseconds = milliseconds;

    /// <summary>No deadline to respect, so Playwright's own default stands.</summary>
    internal static ProbeBudget Unbounded => new ProbeBudget(null);

    /// <summary>
    /// What is left of a step's time, as a Playwright timeout.
    /// </summary>
    /// <remarks>
    /// Never zero. Playwright reads a zero timeout as "wait forever", so the one value that must not be
    /// passed is the one an expired deadline naturally produces - which would turn the fix into the bug it
    /// was written for. A floor of one millisecond means an expired budget fails the call at once.
    /// </remarks>
    /// <param name="deadline">The step's deadline.</param>
    /// <returns>The budget for one poll.</returns>
    internal static ProbeBudget For(StepDeadline deadline)
    {
        ArgumentNullException.ThrowIfNull(deadline);

        return deadline.IsUnbounded
            ? Unbounded
            : new ProbeBudget(Math.Max(1f, (float)deadline.Remaining.TotalMilliseconds));
    }

    /// <summary>The timeout to hand a Playwright call, or <see langword="null"/> to leave its default alone.</summary>
    internal float? Milliseconds => this.milliseconds;
}
