namespace TestFramework.UI.Browser.Configuration;

/// <summary>
/// How much of what the browser saw a run keeps.
/// </summary>
/// <remarks>
/// <para>
/// Evidence is cheap to take and not free to keep: a suite that photographs every click leaves a
/// picture per action per run, and a build agent publishes all of it. So this is a choice the suite
/// makes rather than one the framework makes for it.
/// </para>
/// <para>
/// Whatever is set here, a screenshot a test asked for by name is always taken. That is not a policy
/// about evidence — it is the test saying what it wants recorded, and a setting that could silently
/// discard it would make the verb a lie.
/// </para>
/// </remarks>
public enum UiWidgetCapture
{
    /// <summary>
    /// Photograph only when a step fails or runs out of time.
    /// </summary>
    /// <remarks>
    /// The default, because it is the one that costs nothing on a green suite and everything a reader
    /// needs on a red one.
    /// </remarks>
    OnFailure,

    /// <summary>
    /// Photograph after every action, so a failed step can be read as the sequence that led to it.
    /// </summary>
    /// <remarks>
    /// What turns a step from "the button was not there" into a strip of pictures showing the page it
    /// was actually on. Worth the disk while a flow is being written or a flaky one chased down.
    /// </remarks>
    EveryAction
}
