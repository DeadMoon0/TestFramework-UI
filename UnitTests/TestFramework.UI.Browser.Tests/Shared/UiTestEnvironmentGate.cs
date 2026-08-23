using System;
using System.IO;

namespace TestFramework.UI.Browser.Tests.Shared;

/// <summary>
/// Decides whether the tests that need a real browser can run here.
/// </summary>
/// <remarks>
/// A fresh clone must go green on a bare <c>dotnet test</c>. Everything needing a browser or a built
/// Angular application therefore opts in through an environment variable and skips - visibly, with the
/// reason - when the machine cannot provide it. A skipped test that says why is useful; a failing test
/// that only means "nothing is installed here" is not.
/// </remarks>
internal static class UiTestEnvironmentGate
{
    /// <summary>Names the browser to drive, and opts this machine into the browser tests.</summary>
    public const string BrowserVariable = "TESTFRAMEWORK_UI_BROWSER";

    /// <summary>Set to 1 to let the fixture download browsers if they are missing.</summary>
    public const string AutoInstallVariable = "TESTFRAMEWORK_UI_AUTOINSTALL";

    /// <summary>
    /// What the machine was asked to drive: a browser (<c>chromium</c>, <c>firefox</c>, <c>webkit</c>) or a
    /// branded build (<c>msedge</c>, <c>chrome</c>).
    /// </summary>
    public static string? RequestedBrowser => Environment.GetEnvironmentVariable(BrowserVariable) is { Length: > 0 } value
        ? value
        : null;

    /// <summary>Whether the fixture may install browsers.</summary>
    public static bool MayInstallBrowsers => Environment.GetEnvironmentVariable(AutoInstallVariable) is "1" or "true";

    /// <summary>
    /// Why the browser tests cannot run here, or null when they can.
    /// </summary>
    /// <returns>The reason to skip, or null.</returns>
    public static string? SkipReason()
    {
        if (RequestedBrowser is null)
        {
            return $"Set {BrowserVariable} to run the browser tests - for example {BrowserVariable}=msedge to " +
                   "drive an already-installed Edge, or =chromium to use Playwright's own download.";
        }

        if (!Directory.Exists(AngularOutput))
        {
            return "The Angular sample application has not been built. Run 'npm ci && npm run build' in " +
                   "UnitTests/TestFramework.UI.SampleApp.";
        }

        return null;
    }

    /// <summary>
    /// The build output of the Angular sample application, found relative to the test assembly.
    /// </summary>
    public static string AngularOutput
    {
        get
        {
            // The test assembly sits in bin/<config>/<framework> inside its project, so the sibling project
            // is four levels up. Walking the tree rather than hard-coding it keeps this working whatever
            // configuration or framework the suite runs under.
            DirectoryInfo? directory = new DirectoryInfo(AppContext.BaseDirectory);

            while (directory is not null && !string.Equals(directory.Name, "UnitTests", StringComparison.OrdinalIgnoreCase))
            {
                directory = directory.Parent;
            }

            string root = directory?.FullName ?? AppContext.BaseDirectory;

            return Path.Combine(root, "TestFramework.UI.SampleApp", "dist", "sample-app", "browser");
        }
    }
}
