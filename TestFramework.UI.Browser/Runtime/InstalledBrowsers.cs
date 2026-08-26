using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TestFramework.UI.Browser.Runtime;

/// <summary>
/// A browser this machine can actually drive.
/// </summary>
/// <param name="Browser">What to put in <c>WebAppConfig.Browser</c>, for example <c>chromium</c>.</param>
/// <param name="Channel">What to put in <c>WebAppConfig.Channel</c>, or null for Playwright's own build.</param>
/// <param name="Description">Where it was found, for a fixture that wants to say so.</param>
public sealed record UiAvailableBrowser(string Browser, string? Channel, string Description)
{
    /// <summary>Reads as <c>chromium (Playwright's own build)</c>.</summary>
    /// <returns>The description.</returns>
    public override string ToString() => this.Description;
}

/// <summary>
/// Finds a browser already present on this machine, so nothing has to be told which one to use.
/// </summary>
/// <remarks>
/// <para>
/// This exists because asking was the wrong default. A suite that skips its browser tests unless an
/// environment variable names a browser skips them on every machine that could have run them - which was
/// most of them, including the one this was written on: Edge was installed the whole time and nothing
/// looked. A flag is a fine way to say "use this one"; it is a bad way to answer "can you".
/// </para>
/// <para>
/// Playwright's own build is preferred over a branded one, because its version is pinned by the package
/// reference and a locally installed Edge or Chrome updates itself underneath the suite. Both beat
/// downloading anything: this only ever reports what is already here.
/// </para>
/// <para>
/// Detection is by file path and stays synchronous on purpose - it is called from a test attribute, where
/// starting a driver to ask a question would cost a process per test class.
/// </para>
/// <para>
/// Internal, and reached through <c>BrowserExt.Tooling.FindAvailableBrowser()</c>. Machine preparation
/// already lives there next to <c>InstallBrowsers</c>, which is where someone asking this question would
/// look - and one public way to ask it is the point.
/// </para>
/// </remarks>
internal static class InstalledBrowsers
{
    /// <summary>
    /// Overrides where Playwright keeps its downloaded browsers, and is Playwright's own variable.
    /// </summary>
    private const string BrowsersPathVariable = "PLAYWRIGHT_BROWSERS_PATH";

    /// <summary>
    /// The best browser available here, or null when this machine has none.
    /// </summary>
    /// <returns>The browser to drive, or null.</returns>
    public static UiAvailableBrowser? Find()
    {
        if (FindPlaywrightChromium() is { } downloaded)
        {
            return downloaded;
        }

        foreach ((string channel, string description, IReadOnlyList<string> paths) in BrandedBuilds())
        {
            if (paths.Any(static path => path.Length > 0 && File.Exists(path)))
            {
                return new UiAvailableBrowser("chromium", channel, description);
            }
        }

        return null;
    }

    /// <summary>
    /// The browser named by a channel, when that one specifically is present.
    /// </summary>
    /// <remarks>
    /// For honouring an explicit request: a machine told to use <c>msedge</c> should skip rather than
    /// quietly drive something else, because a suite pinned to a branded build is usually pinned for a
    /// reason.
    /// </remarks>
    /// <param name="requested">A channel (<c>msedge</c>, <c>chrome</c>) or a browser (<c>chromium</c>,
    /// <c>firefox</c>, <c>webkit</c>).</param>
    /// <returns>The browser, or null when the request cannot be met here.</returns>
    public static UiAvailableBrowser? Find(string requested)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requested);

        switch (requested.ToLowerInvariant())
        {
            case "msedge":
            case "edge":
                return FindBranded("msedge");

            case "chrome":
                return FindBranded("chrome");

            // Playwright's own builds. Only chromium's location is known here; firefox and webkit are taken
            // at their word, because a suite naming them has opted into whatever Playwright has downloaded.
            case "chromium":
                return FindPlaywrightChromium();

            case "firefox":
            case "webkit":
                return new UiAvailableBrowser(requested.ToLowerInvariant(), null, $"{requested.ToLowerInvariant()} (Playwright's own build, as requested)");

            default:
                return null;
        }
    }

    private static UiAvailableBrowser? FindBranded(string channel)
        => BrandedBuilds()
            .Where(candidate => string.Equals(candidate.Channel, channel, StringComparison.OrdinalIgnoreCase))
            .Where(static candidate => candidate.Paths.Any(static path => path.Length > 0 && File.Exists(path)))
            .Select(static candidate => new UiAvailableBrowser("chromium", candidate.Channel, candidate.Description))
            .FirstOrDefault();

    /// <summary>
    /// Playwright's downloaded chromium, found the way Playwright itself lays it out.
    /// </summary>
    /// <remarks>
    /// The folder carries the build number (<c>chromium-1234</c>), so this looks for any of them rather
    /// than pinning one: which build the package reference wants is Playwright's business, and a mismatch
    /// is its own clear error at launch rather than something to second-guess here.
    /// </remarks>
    private static UiAvailableBrowser? FindPlaywrightChromium()
    {
        foreach (string root in BrowserCacheRoots())
        {
            if (root.Length == 0 || !Directory.Exists(root))
            {
                continue;
            }

            bool present = Directory
                .EnumerateDirectories(root, "chromium-*", SearchOption.TopDirectoryOnly)
                .Any();

            if (present)
            {
                return new UiAvailableBrowser("chromium", null, "chromium (Playwright's own build)");
            }
        }

        return null;
    }

    private static IEnumerable<string> BrowserCacheRoots()
    {
        if (Environment.GetEnvironmentVariable(BrowsersPathVariable) is { Length: > 0 } configured)
        {
            yield return configured;

            // Playwright treats "0" as "beside the package", which this cannot locate. Saying nothing is
            // then the honest answer rather than reporting the default cache it is not using.
            yield break;
        }

        if (OperatingSystem.IsWindows())
        {
            yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ms-playwright");

            yield break;
        }

        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        if (OperatingSystem.IsMacOS())
        {
            yield return Path.Combine(home, "Library", "Caches", "ms-playwright");

            yield break;
        }

        yield return Path.Combine(home, ".cache", "ms-playwright");
    }

    /// <summary>
    /// Where a branded build lives, per platform.
    /// </summary>
    /// <remarks>
    /// Edge before Chrome, for no better reason than that a Windows machine has Edge whether anyone chose
    /// it or not, so it is the likelier hit and the one that needs no installation.
    /// </remarks>
    private static IReadOnlyList<(string Channel, string Description, IReadOnlyList<string> Paths)> BrandedBuilds()
    {
        if (OperatingSystem.IsWindows())
        {
            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

            return
            [
                ("msedge", "chromium via the installed Microsoft Edge",
                    [
                        Path.Combine(programFilesX86, "Microsoft", "Edge", "Application", "msedge.exe"),
                        Path.Combine(programFiles, "Microsoft", "Edge", "Application", "msedge.exe"),
                    ]),
                ("chrome", "chromium via the installed Google Chrome",
                    [
                        Path.Combine(programFiles, "Google", "Chrome", "Application", "chrome.exe"),
                        Path.Combine(programFilesX86, "Google", "Chrome", "Application", "chrome.exe"),
                    ]),
            ];
        }

        if (OperatingSystem.IsMacOS())
        {
            return
            [
                ("msedge", "chromium via the installed Microsoft Edge",
                    ["/Applications/Microsoft Edge.app/Contents/MacOS/Microsoft Edge"]),
                ("chrome", "chromium via the installed Google Chrome",
                    ["/Applications/Google Chrome.app/Contents/MacOS/Google Chrome"]),
            ];
        }

        return
        [
            ("msedge", "chromium via the installed Microsoft Edge",
                ["/usr/bin/microsoft-edge", "/usr/bin/microsoft-edge-stable", "/opt/microsoft/msedge/msedge"]),
            ("chrome", "chromium via the installed Google Chrome",
                ["/usr/bin/google-chrome", "/usr/bin/google-chrome-stable", "/opt/google/chrome/chrome"]),
        ];
    }
}
