using System;
using TestFramework.UI.Browser.Resolution;

namespace TestFramework.UI.Browser.Configuration;

/// <summary>
/// Everything about how one application is reached and how the browser showing it behaves.
/// </summary>
/// <remarks>
/// <para>
/// The browser environment lives here rather than in the test on purpose. A site does not behave the
/// same at 390 pixels as at 1920 - it collapses navigation, hides columns, swaps a table for cards -
/// and both are worth testing. Pointing a timeline at a second configuration entry covers the second
/// case without a second test, and without a branch inside the first.
/// </para>
/// </remarks>
public sealed record WebAppConfig
{
    /// <summary>
    /// The address the application is reached at. Relative paths in steps resolve against it.
    /// </summary>
    /// <remarks>
    /// An explicit address always wins, so leave it unset when the address should come from a
    /// configured or environment-published resource: the same entry then serves a deployed and a
    /// containerized run, differing only in what wrote the resource's address.
    /// </remarks>
    public string? BaseUrl { get; init; }

    /// <summary>
    /// The identifier of a REST API whose configured address to use instead, for an application the
    /// TestFramework.Web family serves itself. Requires the bridge package.
    /// </summary>
    public string? BaseUrlFromApi { get; init; }

    /// <summary>
    /// The identifier of a configured site whose address to use instead, when it differs from this
    /// application's own identifier. Requires the bridge package. With matching names nothing has to
    /// be set: the application's own identifier is looked up in the site configuration by itself.
    /// </summary>
    public string? BaseUrlFromSite { get; init; }

    /// <summary>
    /// Another entry to inherit every unset value from. Lets a mobile variant be one line rather than a
    /// copy that drifts.
    /// </summary>
    public string? BasedOn { get; init; }

    /// <summary>Which browser to drive: <c>chromium</c>, <c>firefox</c> or <c>webkit</c>.</summary>
    public string? Browser { get; init; }

    /// <summary>
    /// The browser this entry states, for the code that starts one.
    /// </summary>
    /// <remarks>
    /// Everything that reads this comes from <c>UiConfigStore.Get</c>, which refuses an entry that states no
    /// browser - so reaching here without one is a framework bug rather than a configuration mistake, and it
    /// says so. One place asserting that instead of four reads each assuming it.
    /// </remarks>
    internal string StatedBrowser => this.Browser is { Length: > 0 } browser
        ? browser
        : throw new InvalidOperationException(
            "A web application configuration reached the browser factory without a stated browser. "
            + "UiConfigStore.Get refuses that, so this is a bug in TestFramework.UI.Browser rather than in a test.");

    /// <summary>
    /// A branded build to use instead of the downloaded one, for example <c>msedge</c> or <c>chrome</c>.
    /// The way to run with nothing to install on a machine that already has one.
    /// </summary>
    public string? Channel { get; init; }

    /// <summary>Whether the browser runs without a visible window.</summary>
    public bool Headless { get; init; } = true;

    /// <summary>
    /// A named device to emulate: one of Playwright's descriptors such as <c>iPhone 14</c> or
    /// <c>Pixel 7</c>, or one of this package's desktop presets. See <see cref="UiDeviceProfiles"/>.
    /// </summary>
    public string? Device { get; init; }

    /// <summary>The viewport width in pixels. Overrides the device's own width when both are set.</summary>
    public int? ViewportWidth { get; init; }

    /// <summary>The viewport height in pixels. Overrides the device's own height when both are set.</summary>
    public int? ViewportHeight { get; init; }

    /// <summary>The user agent to send, when it should differ from the browser's own.</summary>
    public string? UserAgent { get; init; }

    /// <summary>Whether the page should believe it is on a mobile device.</summary>
    public bool? IsMobile { get; init; }

    /// <summary>Whether touch events are available.</summary>
    public bool? HasTouch { get; init; }

    /// <summary>The device pixel ratio.</summary>
    public float? DeviceScaleFactor { get; init; }

    /// <summary>The locale the page is rendered in, for example <c>de-DE</c>.</summary>
    public string? Locale { get; init; }

    /// <summary>The colour scheme to report: <c>light</c>, <c>dark</c> or <c>no-preference</c>.</summary>
    public string? ColorScheme { get; init; }

    /// <summary>
    /// How long to slow each interaction down by, for watching a run happen. Zero in a normal run.
    /// </summary>
    public TimeSpan SlowMo { get; init; }

    /// <summary>
    /// How long a single interaction may take before it fails. Kept well below a step's own timeout so
    /// the failure names the action rather than the step.
    /// </summary>
    public TimeSpan DefaultActionTimeout { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// How long a comparison against the live page may keep retrying while the page settles.
    /// </summary>
    public TimeSpan DefaultCompareTimeout { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>The attribute a test id target reads. </summary>
    public string TestIdAttribute { get; init; } = "data-testid";

    /// <summary>What to do when a name matches several elements.</summary>
    public UiAmbiguityMode AmbiguityMode { get; init; } = UiAmbiguityMode.Strict;

    /// <summary>Whether to accept certificates a browser would otherwise refuse.</summary>
    public bool IgnoreHttpsErrors { get; init; }

    /// <summary>
    /// Fills every value this entry leaves unset from the entry it is based on.
    /// </summary>
    /// <param name="parent">The entry to inherit from.</param>
    /// <returns>The combined configuration.</returns>
    public WebAppConfig InheritFrom(WebAppConfig parent)
    {
        ArgumentNullException.ThrowIfNull(parent);

        // Only values this entry never mentioned are taken over, so a variant that says
        // "Device: iPhone 14" keeps everything else its parent established.
        return new WebAppConfig
        {
            BaseUrl = this.BaseUrl ?? parent.BaseUrl,
            BaseUrlFromApi = this.BaseUrlFromApi ?? parent.BaseUrlFromApi,
            BaseUrlFromSite = this.BaseUrlFromSite ?? parent.BaseUrlFromSite,
            BasedOn = null,
            Browser = this.Browser ?? parent.Browser,
            Channel = this.Channel ?? parent.Channel,
            Headless = this.Headless == Defaults.Headless ? parent.Headless : this.Headless,
            Device = this.Device ?? parent.Device,
            ViewportWidth = this.ViewportWidth ?? parent.ViewportWidth,
            ViewportHeight = this.ViewportHeight ?? parent.ViewportHeight,
            UserAgent = this.UserAgent ?? parent.UserAgent,
            IsMobile = this.IsMobile ?? parent.IsMobile,
            HasTouch = this.HasTouch ?? parent.HasTouch,
            DeviceScaleFactor = this.DeviceScaleFactor ?? parent.DeviceScaleFactor,
            Locale = this.Locale ?? parent.Locale,
            ColorScheme = this.ColorScheme ?? parent.ColorScheme,
            SlowMo = this.SlowMo == default ? parent.SlowMo : this.SlowMo,
            DefaultActionTimeout = this.DefaultActionTimeout == Defaults.ActionTimeout
                ? parent.DefaultActionTimeout
                : this.DefaultActionTimeout,
            DefaultCompareTimeout = this.DefaultCompareTimeout == Defaults.CompareTimeout
                ? parent.DefaultCompareTimeout
                : this.DefaultCompareTimeout,
            TestIdAttribute = this.TestIdAttribute == Defaults.TestIdAttribute
                ? parent.TestIdAttribute
                : this.TestIdAttribute,
            AmbiguityMode = this.AmbiguityMode == UiAmbiguityMode.Strict ? parent.AmbiguityMode : this.AmbiguityMode,
            IgnoreHttpsErrors = this.IgnoreHttpsErrors || parent.IgnoreHttpsErrors,
        };
    }

    /// <summary>
    /// The values a fresh entry starts with, named so inheritance can tell "left unset" from
    /// "deliberately set to what happens to be the default".
    /// </summary>
    private static class Defaults
    {
        public const bool Headless = true;
        public const string TestIdAttribute = "data-testid";
        public static readonly TimeSpan ActionTimeout = TimeSpan.FromSeconds(10);
        public static readonly TimeSpan CompareTimeout = TimeSpan.FromSeconds(5);
    }
}
