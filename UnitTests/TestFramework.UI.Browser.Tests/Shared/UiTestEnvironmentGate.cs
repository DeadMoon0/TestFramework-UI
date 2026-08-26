using System;
using System.IO;
using TestFramework.UI.Browser.Runtime;

namespace TestFramework.UI.Browser.Tests.Shared;

/// <summary>
/// Decides whether the tests that need a real browser can run here.
/// </summary>
/// <remarks>
/// <para>
/// A fresh clone must go green on a bare <c>dotnet test</c>, so anything needing a browser or a built
/// Angular application skips - visibly, with the reason - when the machine cannot provide it. What changed
/// is how that question is answered: the suite now <em>looks</em> for a browser instead of waiting to be
/// told about one.
/// </para>
/// <para>
/// The old gate skipped unless <c>TESTFRAMEWORK_UI_BROWSER</c> named a browser, which meant 72 of this
/// project's tests - the ones carrying nearly all of its value - sat out on every machine that had not been
/// let in on the secret. Including the machine this was written on, where Edge was installed the whole time.
/// A skip reason of "you did not ask" is not information; "no browser on this machine" is.
/// </para>
/// <para>
/// The variable still works and now means what it says: drive <em>this</em> browser. A machine told to use
/// <c>msedge</c> and lacking it skips rather than quietly driving something else, because a suite pinned to
/// a branded build is usually pinned for a reason.
/// </para>
/// </remarks>
internal static class UiTestEnvironmentGate
{
    /// <summary>Names a specific browser to drive, when the default choice is not wanted.</summary>
    public const string BrowserVariable = "TESTFRAMEWORK_UI_BROWSER";

    /// <summary>Set to 1 to let the fixture download browsers if they are missing.</summary>
    public const string AutoInstallVariable = "TESTFRAMEWORK_UI_AUTOINSTALL";

    private static readonly Lazy<UiAvailableBrowser?> Present = new Lazy<UiAvailableBrowser?>(Look);
    private static readonly Lazy<UiAvailableBrowser?> Intended = new Lazy<UiAvailableBrowser?>(Choose);

    /// <summary>
    /// What was asked for, when anything was.
    /// </summary>
    public static string? RequestedBrowser => Environment.GetEnvironmentVariable(BrowserVariable) is { Length: > 0 } value
        ? value
        : null;

    /// <summary>Whether the fixture may install browsers.</summary>
    public static bool MayInstallBrowsers => Environment.GetEnvironmentVariable(AutoInstallVariable) is "1" or "true";

    /// <summary>
    /// The browser that is on this machine right now, or null when none is.
    /// </summary>
    /// <remarks>
    /// Resolved once per process. Probing the file system per test attribute would be wasteful, and a
    /// browser appearing halfway through a run is not a case worth supporting - with the one exception
    /// below, which is a browser this fixture puts there itself.
    /// </remarks>
    public static UiAvailableBrowser? InstalledBrowser => Present.Value;

    /// <summary>
    /// The browser these tests will drive, counting one this run is allowed to download.
    /// </summary>
    /// <remarks>
    /// The two are separate because of an ordering that is easy to get wrong, and was: a test attribute is
    /// constructed while tests are being discovered, which is before any fixture has run. So a gate that
    /// asked only "is a browser installed" would skip every test, and the fixture that was about to download
    /// one would never get the chance - making <c>TESTFRAMEWORK_UI_AUTOINSTALL</c> dead. A machine that has
    /// opted into downloading a browser therefore counts as having one, and the fixture is what makes that
    /// true. If the download then fails, the tests fail loudly, which is right: this machine asked for it.
    /// </remarks>
    public static UiAvailableBrowser? Browser => Intended.Value;

    /// <summary>
    /// Why the browser tests cannot run here, or null when they can.
    /// </summary>
    /// <returns>The reason to skip, or null.</returns>
    public static string? SkipReason()
    {
        if (Browser is null)
        {
            return RequestedBrowser is { } requested
                ? $"{BrowserVariable} asks for '{requested}', which is not installed on this machine. "
                  + $"Install it, name one that is, or set {AutoInstallVariable}=1 with {BrowserVariable}=chromium to download one."
                : "No browser was found on this machine. Install Edge or Chrome, or run once with "
                  + $"{AutoInstallVariable}=1 to let Playwright download its own.";
        }

        if (!Directory.Exists(AngularOutput))
        {
            return "The Angular sample application has not been built. Run 'npm ci && npm run build' in "
                   + "UnitTests/TestFramework.UI.SampleApp.";
        }

        return null;
    }

    /// <summary>
    /// Honours an explicit request, and otherwise takes what the machine has.
    /// </summary>
    private static UiAvailableBrowser? Look()
        => RequestedBrowser is { } requested
            ? BrowserExt.Tooling.FindAvailableBrowser(requested)
            : BrowserExt.Tooling.FindAvailableBrowser();

    /// <summary>
    /// What is here, or what this run is allowed to fetch.
    /// </summary>
    private static UiAvailableBrowser? Choose()
    {
        if (Look() is { } present)
        {
            return present;
        }

        // Only chromium, and only when asked: it is the one Playwright downloads by default, and a machine
        // that named a branded build wants that build rather than a substitute for it.
        return MayInstallBrowsers && RequestedBrowser is null or "chromium"
            ? new UiAvailableBrowser("chromium", null, "chromium (Playwright will download it for this run)")
            : null;
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
