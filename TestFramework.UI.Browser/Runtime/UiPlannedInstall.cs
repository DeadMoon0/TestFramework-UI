namespace TestFramework.UI.Browser.Runtime;

/// <summary>
/// A browser build the plan found missing and an application asked the run to install.
/// </summary>
/// <param name="Engine"><c>chromium</c>, <c>firefox</c> or <c>webkit</c>.</param>
/// <param name="Headless">Whether the launch is headless: chromium's headless shell and full browser are separate builds.</param>
/// <param name="Build">The build folder the install has to produce, or null when that cannot be told here.</param>
internal sealed record UiPlannedInstall(string Engine, bool Headless, string? Build)
{
    /// <summary>
    /// What Playwright's installer is asked for: only the headless shell when that is all a headless chromium
    /// launch needs, which is a fraction of the full download.
    /// </summary>
    public string[] InstallerArguments => this.Engine == "chromium" && this.Headless
        ? ["install", "--only-shell", "chromium"]
        : ["install", this.Engine];
}
