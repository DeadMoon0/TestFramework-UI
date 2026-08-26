using System;
using System.Collections.Generic;
using TestFramework.Core.Steps;
using TestFramework.Core.Variables;
using TestFramework.UI.Browser.Events;
using TestFramework.UI.Browser.Identifier;
using TestFramework.UI.Browser.Layouting;
using TestFramework.UI.Browser.Runtime;
using TestFramework.UI.Browser.Scripting;
using TestFramework.UI.Browser.Steps;
using TestFramework.UI.Browser.Steps.Inspection;
using TestFramework.UI.Browser.Structure;
using TestFramework.UI.Browser.Targeting;
using TestFramework.UI.Structure;

namespace TestFramework.UI.Browser;

/// <summary>
/// Browser steps for a timeline.
/// </summary>
public static class BrowserExt
{
    /// <summary>
    /// Drives an application in a browser.
    /// </summary>
    /// <remarks>
    /// The returned flow is itself the step, so a timeline reads
    /// <c>.Trigger(BrowserExt.Session("shop").Navigate("/products").Click("Anvil"))</c> with nothing to
    /// finish or build. Steps naming the same application share one browser session for the run, so the
    /// page a step leaves behind is the page the next one finds.
    /// </remarks>
    /// <param name="app">The configured application to drive.</param>
    /// <returns>A flow to add actions to.</returns>
    public static UiBrowserFlow Session(WebAppIdentifier app) => new UiBrowserFlow(app);

    /// <summary>
    /// Looks at what a page is built like, rather than acting on it.
    /// </summary>
    /// <remarks>
    /// Each of these is its own step, so "the order list is right" is a named thing a run either upheld or
    /// did not, rather than an assertion buried inside the flow that produced the page.
    /// </remarks>
    /// <param name="app">The application to look at.</param>
    /// <returns>The inspections available on it.</returns>
    public static PageProxy Page(WebAppIdentifier app) => new PageProxy(app);

    /// <summary>
    /// Things a timeline can wait for on a page, between its steps.
    /// </summary>
    /// <remarks>
    /// A flow's own <c>Expect</c> covers "the page caught up with what I just did". These are for the
    /// state another actor produces - a different step, a background job, a redirect - so the waiting
    /// stands in the timeline where that actor is visible, with its own name and timeout.
    /// </remarks>
    public static UiEventProxy Events { get; } = new UiEventProxy();

    /// <summary>
    /// Things a fixture does around a suite, which are not steps.
    /// </summary>
    public static UiToolingProxy Tooling { get; } = new UiToolingProxy();

    /// <summary>
    /// The waits available on a page.
    /// </summary>
    public sealed class UiEventProxy
    {
        internal UiEventProxy()
        {
        }

        /// <summary>
        /// Completes when an element is on the page and visible.
        /// </summary>
        /// <param name="app">The application to watch.</param>
        /// <param name="target">The element. A plain string names visible text.</param>
        /// <param name="pollDelay">The delay between polls. Defaults to 500 ms.</param>
        /// <returns>The event, for <c>WaitForEvent</c>.</returns>
        public UiElementVisibleEvent ElementVisible(WebAppIdentifier app, UiTarget target, VariableReference<TimeSpan>? pollDelay = null)
            => new UiElementVisibleEvent(app, target, pollDelay);

        /// <summary>
        /// Completes when an element is gone from the page, or was never there.
        /// </summary>
        /// <param name="app">The application to watch.</param>
        /// <param name="target">The element. A plain string names visible text.</param>
        /// <param name="pollDelay">The delay between polls. Defaults to 500 ms.</param>
        /// <returns>The event, for <c>WaitForEvent</c>.</returns>
        public UiElementHiddenEvent ElementHidden(WebAppIdentifier app, UiTarget target, VariableReference<TimeSpan>? pollDelay = null)
            => new UiElementHiddenEvent(app, target, pollDelay);

        /// <summary>
        /// Completes when a text is visible on the page.
        /// </summary>
        /// <param name="app">The application to watch.</param>
        /// <param name="text">The words to wait for.</param>
        /// <param name="pollDelay">The delay between polls. Defaults to 500 ms.</param>
        /// <returns>The event, for <c>WaitForEvent</c>.</returns>
        public UiElementVisibleEvent TextAppears(WebAppIdentifier app, string text, VariableReference<TimeSpan>? pollDelay = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(text);

            return new UiElementVisibleEvent(app, Target.Text(text), pollDelay);
        }

        /// <summary>
        /// Completes when a text is gone from the page, or was never there.
        /// </summary>
        /// <param name="app">The application to watch.</param>
        /// <param name="text">The words that must go away.</param>
        /// <param name="pollDelay">The delay between polls. Defaults to 500 ms.</param>
        /// <returns>The event, for <c>WaitForEvent</c>.</returns>
        public UiElementHiddenEvent TextDisappears(WebAppIdentifier app, string text, VariableReference<TimeSpan>? pollDelay = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(text);

            return new UiElementHiddenEvent(app, Target.Text(text), pollDelay);
        }

        /// <summary>
        /// Completes when an attribute of an element reads a value a rule accepts.
        /// </summary>
        /// <remarks>
        /// The channel components report on without a visible word changing - <c>data-state</c>,
        /// <c>aria-busy</c>, <c>aria-expanded</c>. A plain string for <paramref name="expected"/> means
        /// exact; <c>Cell.Contains</c>, <c>Cell.Matches</c> and the other rules say anything looser.
        /// </remarks>
        /// <param name="app">The application to watch.</param>
        /// <param name="target">The element carrying the attribute.</param>
        /// <param name="attribute">The attribute name, for example <c>data-state</c>.</param>
        /// <param name="expected">What the value must satisfy.</param>
        /// <param name="pollDelay">The delay between polls. Defaults to 500 ms.</param>
        /// <returns>The event, for <c>WaitForEvent</c>.</returns>
        public UiAttributeEqualsEvent AttributeEquals(
            WebAppIdentifier app,
            UiTarget target,
            string attribute,
            CellRule expected,
            VariableReference<TimeSpan>? pollDelay = null)
            => new UiAttributeEqualsEvent(app, target, attribute, expected, pollDelay);

        /// <summary>
        /// Completes when an attribute of an element no longer reads what the wait first saw.
        /// </summary>
        /// <remarks>
        /// For transitions with no destination value a test could name - a stamp, a counter, a rotating
        /// id. The baseline is taken on the wait's first look, so the wait must start before the change.
        /// When the destination is known, <see cref="AttributeEquals"/> says it better.
        /// </remarks>
        /// <param name="app">The application to watch.</param>
        /// <param name="target">The element carrying the attribute.</param>
        /// <param name="attribute">The attribute name.</param>
        /// <param name="pollDelay">The delay between polls. Defaults to 500 ms.</param>
        /// <returns>The event, for <c>WaitForEvent</c>.</returns>
        public UiAttributeChangedEvent AttributeChanged(
            WebAppIdentifier app,
            UiTarget target,
            string attribute,
            VariableReference<TimeSpan>? pollDelay = null)
            => new UiAttributeChangedEvent(app, target, attribute, pollDelay);

        /// <summary>
        /// Completes when exactly <paramref name="count"/> elements answer to a target.
        /// </summary>
        /// <remarks>
        /// For lists that fill in. A page can pass through the expected count on its way past it and
        /// satisfy this wait; a list whose final size matters more than the moment it is reached is
        /// better waited on with <see cref="StructureMatches"/> or <see cref="TableMatches"/>.
        /// </remarks>
        /// <param name="app">The application to watch.</param>
        /// <param name="target">What to count.</param>
        /// <param name="count">The count to reach. Zero waits for absence.</param>
        /// <param name="pollDelay">The delay between polls. Defaults to 500 ms.</param>
        /// <returns>The event, for <c>WaitForEvent</c>.</returns>
        public UiElementCountEvent CountIs(WebAppIdentifier app, UiTarget target, int count, VariableReference<TimeSpan>? pollDelay = null)
            => new UiElementCountEvent(app, target, count, atLeast: false, pollDelay);

        /// <summary>
        /// Completes when at least <paramref name="count"/> elements answer to a target.
        /// </summary>
        /// <param name="app">The application to watch.</param>
        /// <param name="target">What to count.</param>
        /// <param name="count">The count to reach.</param>
        /// <param name="pollDelay">The delay between polls. Defaults to 500 ms.</param>
        /// <returns>The event, for <c>WaitForEvent</c>.</returns>
        public UiElementCountEvent CountAtLeast(WebAppIdentifier app, UiTarget target, int count, VariableReference<TimeSpan>? pollDelay = null)
            => new UiElementCountEvent(app, target, count, atLeast: true, pollDelay);

        /// <summary>
        /// Completes when the page's address contains a text - or matches an expression, with
        /// <see cref="UiUrlMatchesEvent.AsRegex"/>.
        /// </summary>
        /// <param name="app">The application to watch.</param>
        /// <param name="pattern">The substring, or the expression.</param>
        /// <param name="pollDelay">The delay between polls. Defaults to 500 ms.</param>
        /// <returns>The event, for <c>WaitForEvent</c>.</returns>
        public UiUrlMatchesEvent UrlMatches(WebAppIdentifier app, VariableReference<string> pattern, VariableReference<TimeSpan>? pollDelay = null)
            => new UiUrlMatchesEvent(app, pattern, pollDelay);

        /// <summary>
        /// Completes when a part of the page is built the way a structure says.
        /// </summary>
        /// <remarks>
        /// The same comparison the <c>CompareStructure</c> step makes, as a wait - for the shape another
        /// actor produces over time. A timeout reports the last look's differences, so a wait that never
        /// matched ends as readably as a comparison that failed.
        /// </remarks>
        /// <param name="app">The application to watch.</param>
        /// <param name="scope">The element to compare, and everything inside it.</param>
        /// <param name="expected">The structure it should have.</param>
        /// <param name="pollDelay">The delay between polls. Defaults to 500 ms.</param>
        /// <returns>The event, for <c>WaitForEvent</c>.</returns>
        public UiStructureMatchesEvent StructureMatches(
            WebAppIdentifier app,
            UiTarget scope,
            WebElementStructure expected,
            VariableReference<TimeSpan>? pollDelay = null)
            => new UiStructureMatchesEvent(app, scope, expected, pollDelay);

        /// <summary>
        /// Completes when a table on the page holds what an expected table says.
        /// </summary>
        /// <param name="app">The application to watch.</param>
        /// <param name="table">The table element.</param>
        /// <param name="expected">The rows it should hold.</param>
        /// <param name="pollDelay">The delay between polls. Defaults to 500 ms.</param>
        /// <returns>The event, for <c>WaitForEvent</c>.</returns>
        public UiTableMatchesEvent TableMatches(
            WebAppIdentifier app,
            UiTarget table,
            ExpectedTable expected,
            VariableReference<TimeSpan>? pollDelay = null)
            => new UiTableMatchesEvent(app, table, expected, pollDelay);

        /// <summary>
        /// Completes when a script in the page returns <c>true</c>.
        /// </summary>
        /// <param name="app">The application to watch.</param>
        /// <param name="script">The question, phrased to always have a boolean answer - for example
        /// <c>() =&gt; window.myApp?.ready === true</c>.</param>
        /// <param name="pollDelay">The delay between polls. Defaults to 500 ms.</param>
        /// <returns>The event, for <c>WaitForEvent</c>.</returns>
        public UiScriptIsTrueEvent ScriptIsTrue(WebAppIdentifier app, JsScript script, VariableReference<TimeSpan>? pollDelay = null)
            => new UiScriptIsTrueEvent(app, script, pollDelay);
    }

    /// <summary>
    /// The inspections available on one application's page.
    /// </summary>
    public sealed class PageProxy
    {
        private readonly WebAppIdentifier app;

        internal PageProxy(WebAppIdentifier app)
        {
            ArgumentNullException.ThrowIfNull(app);

            this.app = app;
        }

        /// <summary>
        /// Checks that a part of the page is built the way a structure says.
        /// </summary>
        /// <remarks>
        /// Retried until the page settles, so a comparison against an application that renders after
        /// fetching is not a race.
        /// </remarks>
        /// <param name="scope">The element to compare, and everything inside it.</param>
        /// <param name="expected">The structure it should have.</param>
        /// <returns>The step.</returns>
        public Step<UiCompareResultContext> CompareStructure(UiTarget scope, WebElementStructure expected)
            => new CompareStructureStep(this.app, scope, expected);

        /// <summary>
        /// Checks that a table holds what an expected table says.
        /// </summary>
        /// <param name="table">The table element.</param>
        /// <param name="expected">The rows it should hold.</param>
        /// <returns>The step.</returns>
        public Step<UiCompareResultContext> CompareTable(UiTarget table, ExpectedTable expected)
            => new CompareTableStep(this.app, table, expected);

        /// <summary>
        /// Reads a table off the page as data.
        /// </summary>
        /// <param name="table">The table element.</param>
        /// <param name="into">A variable to put the rows in, or null to only return them as the step's result.</param>
        /// <returns>The step.</returns>
        public Step<UiTableResultContext> ReadTable(UiTarget table, VariableIdentifier? into = null)
            => new ReadTableStep(this.app, table, into);

        /// <summary>
        /// Checks that the page is laid out the way a layout says.
        /// </summary>
        /// <remarks>
        /// Relations between the things a person sees - above, left of, inside, not overlapping - rather
        /// than coordinates, with a built-in tolerance for rounding. Retried until the page settles; a
        /// failure lists every violated relation with both actual rectangles.
        /// </remarks>
        /// <param name="expected">The relations the page must satisfy.</param>
        /// <returns>The step.</returns>
        public Step<UiCompareResultContext> CheckLayout(ExpectedLayout expected)
            => new CheckLayoutStep(this.app, expected);

        /// <summary>
        /// Records where everything in a part of the page sits, so a later run can be told when the
        /// geometry changed.
        /// </summary>
        /// <remarks>
        /// Positions are relative to the scope and snapped to a four-pixel grid, so only a change a
        /// person could point at reads as drift - no pixel files, no baseline service.
        /// </remarks>
        /// <param name="scope">The element whose contents are recorded.</param>
        /// <param name="name">The variable to record them in.</param>
        /// <returns>The step.</returns>
        public Step<UiCaptureResultContext> CaptureLayout(UiTarget scope, string name)
            => new CaptureLayoutStep(this.app, scope, name);

        /// <summary>
        /// Records what a part of the page is built like, so a later run can be told when it changed.
        /// </summary>
        /// <param name="scope">The element to record, and everything inside it.</param>
        /// <param name="name">The variable to record it in.</param>
        /// <returns>The step.</returns>
        public Step<UiCaptureResultContext> CaptureStructure(UiTarget scope, string name)
            => new CaptureStructureStep(this.app, scope, name);
    }

    /// <summary>
    /// Machine preparation, kept out of timelines on purpose.
    /// </summary>
    public sealed class UiToolingProxy
    {
        /// <summary>
        /// Downloads the browsers Playwright drives, if they are not already present.
        /// </summary>
        /// <remarks>
        /// Not a step, and deliberately so: installing software is provisioning a machine, not testing an
        /// application. Inside a timeline it would make the first run of a suite mutate the developer's
        /// machine and would charge the download to a step's timing and retries. Call it from a fixture,
        /// or set <c>"Channel": "msedge"</c> and use a browser that is already installed.
        /// </remarks>
        /// <param name="browsers">Which to install, for example <c>chromium</c>. Installs all when empty.</param>
        /// <returns>The exit code of the installer; zero means success.</returns>
        public int InstallBrowsers(params string[] browsers)
        {
            List<string> arguments = new List<string> { "install" };

            if (browsers is { Length: > 0 })
            {
                arguments.AddRange(browsers);
            }

            return Microsoft.Playwright.Program.Main([.. arguments]);
        }

        /// <summary>
        /// A browser this machine can already drive, or null when it has none.
        /// </summary>
        /// <remarks>
        /// For a fixture deciding whether to run browser tests at all, and for one that wants to say which
        /// browser it picked. Playwright's own build is preferred, because its version is pinned by the
        /// package reference while an installed Edge or Chrome updates itself underneath a suite; an
        /// installed branded build is the fallback. Nothing here downloads anything - it reports what is
        /// present.
        /// </remarks>
        /// <returns>The browser to drive, or null.</returns>
        public UiAvailableBrowser? FindAvailableBrowser() => InstalledBrowsers.Find();

        /// <summary>
        /// The browser named by a channel or engine, when that one specifically is present.
        /// </summary>
        /// <remarks>
        /// For honouring an explicit choice: a suite told to use <c>msedge</c> should skip rather than
        /// quietly drive something else, because a suite pinned to a branded build is usually pinned for a
        /// reason.
        /// </remarks>
        /// <param name="requested">A channel (<c>msedge</c>, <c>chrome</c>) or an engine
        /// (<c>chromium</c>, <c>firefox</c>, <c>webkit</c>).</param>
        /// <returns>The browser, or null when the request cannot be met here.</returns>
        public UiAvailableBrowser? FindAvailableBrowser(string requested) => InstalledBrowsers.Find(requested);

        /// <summary>
        /// The environment variables that change how a browser runs locally, for a fixture that wants to
        /// report them.
        /// </summary>
        /// <returns>The variable names.</returns>
        public IReadOnlyList<string> LocalOverrideVariables =>
        [
            UiEnvironmentOverrides.HeadedVariable,
            UiEnvironmentOverrides.SlowMoVariable,
            UiEnvironmentOverrides.PauseOnFailureVariable,
        ];
    }
}
