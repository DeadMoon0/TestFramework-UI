using System;
using System.Collections.Generic;
using TestFramework.UI.Browser.Identifier;
using TestFramework.UI.Browser.Runtime;
using TestFramework.UI.Browser.Steps;

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
    /// Things a fixture does around a suite, which are not steps.
    /// </summary>
    public static UiToolingProxy Tooling { get; } = new UiToolingProxy();

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
