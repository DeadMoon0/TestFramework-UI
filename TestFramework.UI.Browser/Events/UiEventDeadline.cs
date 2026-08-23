using System;

namespace TestFramework.UI.Browser.Events;

/// <summary>
/// How long before its own step timeout a wait gives up, so its message is the one that survives.
/// </summary>
/// <remarks>
/// The runner awaits a step with a timeout token and abandons the running task the instant it fires - an
/// exception the step raises at that same moment is never observed, and the reader gets the generic
/// "Step timed out" instead of what was being waited for and where the page was. Finishing first is the
/// only way the real message reaches them. The margin is a sixth of the timeout, never below 200 ms
/// (a loaded CI runner swallows less - 50 ms and 100 ms were both tried and lost) and never above a
/// second, so a long wait is not meaningfully shortened. The same arithmetic as the file event's, kept
/// as its own copy because this package builds against the published packages.
/// </remarks>
internal static class UiEventDeadline
{
    /// <summary>
    /// The deadline a wait enforces on itself, measured from the start of polling.
    /// </summary>
    /// <param name="stepTimeout">The step's own timeout.</param>
    /// <returns>The deadline, or zero when the timeout is unset or unbounded and no own deadline is
    /// needed.</returns>
    public static TimeSpan For(TimeSpan stepTimeout)
    {
        if (stepTimeout <= TimeSpan.Zero || stepTimeout > TimeSpan.FromDays(1))
        {
            return TimeSpan.Zero;
        }

        double marginMs = Math.Clamp(stepTimeout.TotalMilliseconds / 6, 200, 1000);

        // A timeout too short to carry the margin keeps a usable slice rather than going negative.
        return marginMs >= stepTimeout.TotalMilliseconds
            ? TimeSpan.FromMilliseconds(stepTimeout.TotalMilliseconds / 2)
            : stepTimeout - TimeSpan.FromMilliseconds(marginMs);
    }
}
