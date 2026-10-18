using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TestFramework.UI.Browser.Runtime;

/// <summary>
/// A browser this machine can actually drive.
/// </summary>
public sealed record UiAvailableBrowser
{
    internal UiAvailableBrowser(string browser, string? channel, string description)
    {
        this.Browser = browser;
        this.Channel = channel;
        this.Description = description;
    }

    /// <summary>
    /// What to put in <c>WebAppConfig.Browser</c>, for example <c>chromium</c>.
    /// </summary>
    public string Browser { get; }

    /// <summary>
    /// What to put in <c>WebAppConfig.Channel</c>, or null for Playwright's own build.
    /// </summary>
    public string? Channel { get; }

    /// <summary>
    /// Where it was found, for a fixture that wants to say so.
    /// </summary>
    public string Description { get; }

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
    /// The best browser available here, or null when this machine has none.
    /// </summary>
    /// <returns>The browser to drive, or null.</returns>
    public static UiAvailableBrowser? Find()
    {
        if (FindPlaywrightBuild("chromium") is { } downloaded)
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

            // Playwright's own builds, each looked for where Playwright downloads it.
            case "chromium":
            case "firefox":
            case "webkit":
                return FindPlaywrightBuild(requested.ToLowerInvariant());

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

    private static UiAvailableBrowser? FindPlaywrightBuild(string engine)
        => PlaywrightBuilds.CacheRoots() is { } roots ? FindPlaywrightBuild(engine, PlaywrightBuilds.Pins, roots) : null;

    /// <summary>
    /// The build of <paramref name="engine"/> this version of Playwright launches, when it is installed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The exact build, not any: a cache restored from an older Playwright holds a finished build of another
    /// number, and reporting it as available sends a suite into a launch that fails. Headless or headed is
    /// the launch's choice, so either of chromium's two builds counts.
    /// </para>
    /// <para>
    /// A folder counts only once Playwright has marked it finished. Its installer writes that marker last,
    /// so an interrupted download - or a folder made by hand - is not reported as a browser this machine
    /// can drive.
    /// </para>
    /// </remarks>
    /// <param name="engine"><c>chromium</c>, <c>firefox</c> or <c>webkit</c>.</param>
    /// <param name="pins">The builds this version of Playwright launches.</param>
    /// <param name="cacheRoots">The folders Playwright keeps its downloads in.</param>
    /// <returns>The build, or null when it is not installed or the pins cannot be read.</returns>
    internal static UiAvailableBrowser? FindPlaywrightBuild(string engine, IReadOnlyDictionary<string, IReadOnlyList<string>>? pins, IReadOnlyList<string> cacheRoots)
        => PlaywrightBuilds.Check(engine, headless: true, pins, cacheRoots).IsInstalled
        || PlaywrightBuilds.Check(engine, headless: false, pins, cacheRoots).IsInstalled
            ? new UiAvailableBrowser(engine, null, $"{engine} (Playwright's own build)")
            : null;

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
