using System;
using TestFramework.Config.Configuration;
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
public sealed record WebAppConfig : IInheritsConfig
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
    /// TestFramework.Web family serves itself. Resolves through the run; no bridge package involved.
    /// </summary>
    public string? BaseUrlFromApi { get; init; }

    /// <summary>
    /// The identifier of a configured site whose address to use instead, when it differs from this
    /// application's own identifier. Resolves through the run; no bridge package involved. With
    /// matching names nothing has to be set: the application's own identifier is looked up in the
    /// site configuration by itself.
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
    /// The one <c>Effective…</c> value with no default behind it, because §5 of the family's architecture
    /// forbids one here: which browser a test drives decides what it proves. Everything that reads this comes
    /// from <c>UiConfigStore.Get</c>, which refuses an entry that states no browser - so reaching here
    /// without one is a framework bug rather than a configuration mistake, and it says so.
    /// </remarks>
    internal string EffectiveBrowser => this.Browser is { Length: > 0 } browser
        ? browser
        : throw new InvalidOperationException(
            "A web application configuration reached the browser factory without a stated browser. "
            + "UiConfigStore.Get refuses that, so this is a bug in TestFramework.UI.Browser rather than in a test.");

    /// <summary>
    /// A branded build to use instead of the downloaded one, for example <c>msedge</c> or <c>chrome</c>.
    /// The way to run with nothing to install on a machine that already has one.
    /// </summary>
    public string? Channel { get; init; }

    /// <summary>Whether the browser runs without a visible window. Defaults to true.</summary>
    public bool? Headless { get; init; }

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
    public TimeSpan? SlowMo { get; init; }

    /// <summary>
    /// How long a single interaction may take before it fails. Kept well below a step's own timeout so
    /// the failure names the action rather than the step. Defaults to 10 seconds.
    /// </summary>
    public TimeSpan? DefaultActionTimeout { get; init; }

    /// <summary>
    /// How long a comparison against the live page may keep retrying while the page settles.
    /// Defaults to 5 seconds.
    /// </summary>
    public TimeSpan? DefaultCompareTimeout { get; init; }

    /// <summary>The attribute a test id target reads. Defaults to <c>data-testid</c>.</summary>
    public string? TestIdAttribute { get; init; }

    /// <summary>What to do when a name matches several elements. Defaults to refusing.</summary>
    public UiAmbiguityMode? AmbiguityMode { get; init; }

    /// <summary>Whether to accept certificates a browser would otherwise refuse. Defaults to false.</summary>
    public bool? IgnoreHttpsErrors { get; init; }

    /// <summary>
    /// The values that apply when an entry did not state one.
    /// </summary>
    /// <remarks>
    /// Read through the <c>Effective…</c> members below, and applied there rather than in the declaration.
    /// That is the whole reason inheritance is safe now: a declared value of null means "nobody set this",
    /// and a default sitting in the declaration would have made that unrepresentable. It is also what these
    /// used to be for - sentinels the old hand-written merge compared against, which could not tell a
    /// deliberate choice from silence and quietly preferred the parent. The merge is
    /// <c>TestFramework.Config</c>'s now, so there is nothing left to compare against.
    /// </remarks>
    private static class Defaults
    {
        public const bool Headless = true;
        public const string TestIdAttribute = "data-testid";
        public const UiAmbiguityMode Ambiguity = UiAmbiguityMode.Strict;
        public const bool IgnoreHttpsErrors = false;
        public static readonly TimeSpan SlowMo = TimeSpan.Zero;
        public static readonly TimeSpan ActionTimeout = TimeSpan.FromSeconds(10);
        public static readonly TimeSpan CompareTimeout = TimeSpan.FromSeconds(5);
    }

    /// <summary>Whether the browser runs without a visible window, defaulted.</summary>
    internal bool EffectiveHeadless => this.Headless ?? Defaults.Headless;

    /// <summary>How long to slow each interaction down by, defaulted to not at all.</summary>
    internal TimeSpan EffectiveSlowMo => this.SlowMo ?? Defaults.SlowMo;

    /// <summary>How long a single interaction may take, defaulted.</summary>
    internal TimeSpan EffectiveActionTimeout => this.DefaultActionTimeout ?? Defaults.ActionTimeout;

    /// <summary>How long a comparison may keep retrying, defaulted.</summary>
    internal TimeSpan EffectiveCompareTimeout => this.DefaultCompareTimeout ?? Defaults.CompareTimeout;

    /// <summary>The attribute a test id target reads, defaulted.</summary>
    internal string EffectiveTestIdAttribute => this.TestIdAttribute is { Length: > 0 } attribute ? attribute : Defaults.TestIdAttribute;

    /// <summary>What to do when a name matches several elements, defaulted to refusing.</summary>
    internal UiAmbiguityMode EffectiveAmbiguityMode => this.AmbiguityMode ?? Defaults.Ambiguity;

    /// <summary>Whether to accept certificates a browser would otherwise refuse, defaulted.</summary>
    internal bool EffectiveIgnoreHttpsErrors => this.IgnoreHttpsErrors ?? Defaults.IgnoreHttpsErrors;
}
