using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Playwright;
using Newtonsoft.Json.Linq;

namespace TestFramework.UI.Browser.Runtime;

/// <summary>
/// The browser builds this version of Playwright launches, and whether they are on this machine.
/// </summary>
/// <remarks>
/// <para>
/// The Playwright package pins one build of each browser - <c>chromium-1234</c>, not "a chromium" - and
/// writes the pins into <c>.playwright/package/browsers.json</c> beside its assembly. A launch looks for that
/// exact build, so asking whether "some" chromium is installed answers the wrong question: a pipeline cache
/// restored from an older Playwright holds a finished <c>chromium-1223</c>, says yes, and the run fails at its
/// first browser step after everything before it has run.
/// </para>
/// <para>
/// Answered from files, synchronously, because the run is still being planned when it is asked and starting
/// the driver to ask would cost a process.
/// </para>
/// </remarks>
internal static class PlaywrightBuilds
{
    /// <summary>The file Playwright's installer writes into a build's folder once its download is complete.</summary>
    internal const string InstallationCompleteMarker = "INSTALLATION_COMPLETE";

    private static readonly Lazy<IReadOnlyDictionary<string, IReadOnlyList<string>>?> LoadedPins = new(
        static () => ReadPins(PinsFile()));

    /// <summary>Each build this version of Playwright launches, with its acceptable revisions; null when unreadable.</summary>
    internal static IReadOnlyDictionary<string, IReadOnlyList<string>>? Pins => LoadedPins.Value;

    /// <summary>
    /// Whether the build a launch of <paramref name="engine"/> needs is installed here.
    /// </summary>
    /// <param name="engine"><c>chromium</c>, <c>firefox</c> or <c>webkit</c>.</param>
    /// <param name="headless">Whether the launch is headless: chromium then runs its headless shell, a separate download.</param>
    /// <returns>The answer, which is <see cref="UiBuildCheck.Unknown"/> when it cannot be told from here.</returns>
    public static UiBuildCheck Check(string engine, bool headless)
        => CacheRoots() is { } roots ? Check(engine, headless, Pins, roots) : UiBuildCheck.Unknown;

    internal static UiBuildCheck Check(string engine, bool headless, IReadOnlyDictionary<string, IReadOnlyList<string>>? pins, IReadOnlyList<string> cacheRoots)
    {
        string build = BuildOf(engine, headless);
        if (pins is null || !pins.TryGetValue(build, out IReadOnlyList<string>? revisions))
        {
            return UiBuildCheck.Unknown;
        }

        // Playwright names the folder after the build with '-' turned into '_': chromium_headless_shell-1234.
        string prefix = build.Replace('-', '_') + "-";
        string[] accepted = [.. revisions.Select(revision => prefix + revision)];

        if (accepted.Any(folder => cacheRoots.Any(root => IsFinished(Path.Combine(root, folder)))))
        {
            return UiBuildCheck.Installed;
        }

        // What is there instead says why: an older Playwright's build is a stale cache, not a fresh machine.
        string[] others =
        [
            .. cacheRoots
                .Where(Directory.Exists)
                .SelectMany(root => Directory.EnumerateDirectories(root, prefix + "*", SearchOption.TopDirectoryOnly))
                .Where(IsFinished)
                .Select(static folder => Path.GetFileName(folder))
                .Order(StringComparer.Ordinal),
        ];

        return UiBuildCheck.Missing(accepted[0], others);
    }

    /// <summary>Whether a build folder is installed and finished, or null when the cache cannot be seen from here.</summary>
    /// <param name="build">The build folder, for example <c>chromium_headless_shell-1234</c>.</param>
    /// <returns>The answer.</returns>
    public static bool? IsInstalled(string build)
        => CacheRoots() is { } roots ? roots.Any(root => IsFinished(Path.Combine(root, build))) : null;

    /// <summary>
    /// Where Playwright keeps its downloaded browsers, or null when it keeps them somewhere this cannot see.
    /// </summary>
    /// <remarks>
    /// <c>PLAYWRIGHT_BROWSERS_PATH=0</c> means "beside the package", which is not a place this can name; saying
    /// nothing is then the honest answer rather than reporting on a cache the launch does not use.
    /// </remarks>
    internal static IReadOnlyList<string>? CacheRoots()
    {
        if (Environment.GetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH") is { Length: > 0 } configured)
        {
            return configured == "0" ? null : [configured];
        }

        if (OperatingSystem.IsWindows())
        {
            return [Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ms-playwright")];
        }

        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return OperatingSystem.IsMacOS()
            ? [Path.Combine(home, "Library", "Caches", "ms-playwright")]
            : [Path.Combine(home, ".cache", "ms-playwright")];
    }

    /// <summary>
    /// Each build's acceptable revisions: its own, plus any per-platform override Playwright lists for it.
    /// </summary>
    /// <remarks>
    /// The overrides are keyed by an operating-system release this does not try to identify, so any of them is
    /// accepted. That can only make the check say "installed" for a revision Playwright pinned for some platform -
    /// never "missing" for one it would launch.
    /// </remarks>
    /// <param name="path">The pins file, or null when there is none.</param>
    /// <returns>The pins, or null when they cannot be read.</returns>
    internal static IReadOnlyDictionary<string, IReadOnlyList<string>>? ReadPins(string? path)
    {
        if (path is null)
        {
            return null;
        }

        try
        {
            JObject document = JObject.Parse(File.ReadAllText(path));
            Dictionary<string, IReadOnlyList<string>> pins = new(StringComparer.Ordinal);

            foreach (JToken browser in document["browsers"] ?? new JArray())
            {
                if (browser.Value<string>("name") is not { Length: > 0 } name || browser.Value<string>("revision") is not { Length: > 0 } revision)
                {
                    continue;
                }

                IEnumerable<string> overrides = (browser["revisionOverrides"] as JObject)?.Properties()
                    .Select(static property => property.Value.Value<string>())
                    .OfType<string>() ?? [];

                pins[name] = [revision, .. overrides.Where(other => other != revision).Distinct(StringComparer.Ordinal)];
            }

            return pins;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or Newtonsoft.Json.JsonException)
        {
            return null;
        }
    }

    private static string BuildOf(string engine, bool headless) => engine.ToLowerInvariant() switch
    {
        "chromium" => headless ? "chromium-headless-shell" : "chromium",
        "firefox" => "firefox",
        "webkit" => "webkit",
        _ => throw new ArgumentOutOfRangeException(nameof(engine), engine, "Not a Playwright engine."),
    };

    private static bool IsFinished(string folder) => File.Exists(Path.Combine(folder, InstallationCompleteMarker));

    private static string? PinsFile()
    {
        string?[] candidates =
        [
            Path.GetDirectoryName(typeof(IPlaywright).Assembly.Location),
            AppContext.BaseDirectory,
        ];

        return candidates
            .OfType<string>()
            .Where(static directory => directory.Length > 0)
            .Select(static directory => Path.Combine(directory, ".playwright", "package", "browsers.json"))
            .FirstOrDefault(File.Exists);
    }
}
