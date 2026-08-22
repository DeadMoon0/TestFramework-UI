using System;
using System.Collections.Generic;
using TestFramework.UI.Browser.Targeting;

namespace TestFramework.UI.Browser.Resolution;

/// <summary>
/// Which channels to try, in which order, for a given target.
/// </summary>
/// <remarks>
/// <para>
/// The order is the whole resilience story, so it is stated here once rather than spread across the
/// steps. It follows how a person perceives a control - what it is and what it is called, then how it
/// is labelled, then what it hints, then the identifier a developer attached for testing, and last
/// its raw text.
/// </para>
/// <para>
/// A target that names a kind keeps a short ladder on purpose. <c>Target.Button("Save")</c> asks for a
/// button; if the page turned that button into a link, the test should say so rather than quietly
/// carry on, because the two behave differently for keyboard and screen-reader users. A plain
/// <c>"Save"</c> is the forgiving form, and it is the one most tests use.
/// </para>
/// </remarks>
internal static class UiChannelLadder
{
    /// <summary>
    /// The channels to try for a target, strongest first. Each is tried exact before any is tried
    /// loose - the sweeps are applied by <see cref="TargetResolver"/>, not encoded here.
    /// </summary>
    /// <param name="target">The target being looked for.</param>
    /// <param name="context">What a plain string means here.</param>
    /// <returns>The channels, strongest first.</returns>
    public static IReadOnlyList<UiChannelStep> For(UiTarget target, UiSmartContext context)
    {
        ArgumentNullException.ThrowIfNull(target);

        return target.Kind switch
        {
            UiTargetKind.Smart => ForContext(context),

            // Kind named by the test: stay on that role, plus the test id as the escape hatch that is
            // always available. Text is deliberately absent - the test said "button", not "words".
            UiTargetKind.Button => [Role("button"), Channel(UiMatchChannel.TestId)],
            UiTargetKind.Link => [Role("link"), Channel(UiMatchChannel.TestId)],
            UiTargetKind.Field =>
            [
                Channel(UiMatchChannel.Label),
                Channel(UiMatchChannel.Placeholder),
                Role("textbox"),
                Role("combobox"),
                Role("spinbutton"),
                Channel(UiMatchChannel.TestId),
            ],
            UiTargetKind.Checkbox => [Role("checkbox"), Channel(UiMatchChannel.Label), Channel(UiMatchChannel.TestId)],
            UiTargetKind.Radio => [Role("radio"), Channel(UiMatchChannel.Label), Channel(UiMatchChannel.TestId)],
            UiTargetKind.Text => [Channel(UiMatchChannel.Text)],
            UiTargetKind.Role => [Role(target.Role ?? throw new InvalidOperationException("A role target carries no role."))],
            UiTargetKind.Section => SectionLadder,

            // Single-channel targets: the test named the mechanism, so there is nothing to fall back to.
            UiTargetKind.TestId => [Channel(UiMatchChannel.TestId)],
            UiTargetKind.Css => [Channel(UiMatchChannel.Css)],
            UiTargetKind.ElementId => [Channel(UiMatchChannel.ElementId)],

            _ => throw new InvalidOperationException($"Unknown target kind '{target.Kind}'."),
        };
    }

    /// <summary>
    /// True when the channel has no loose form, so the second sweep would only repeat the first.
    /// </summary>
    /// <param name="channel">The channel.</param>
    /// <returns>True when only an exact lookup makes sense.</returns>
    public static bool IsExactOnly(UiMatchChannel channel)
        => channel is UiMatchChannel.Css or UiMatchChannel.ElementId or UiMatchChannel.TestId;

    private static IReadOnlyList<UiChannelStep> ForContext(UiSmartContext context)
        => context switch
        {
            UiSmartContext.Clickable =>
            [
                Role("button"),
                Role("link"),
                Role("menuitem"),
                Role("tab"),
                Channel(UiMatchChannel.TestId),
                Channel(UiMatchChannel.Text),
            ],
            UiSmartContext.Fillable =>
            [
                Channel(UiMatchChannel.Label),
                Channel(UiMatchChannel.Placeholder),
                Role("textbox"),
                Role("combobox"),
                Role("spinbutton"),
                Channel(UiMatchChannel.TestId),
            ],
            UiSmartContext.Checkable =>
            [
                Role("checkbox"),
                Role("radio"),
                Role("switch"),
                Channel(UiMatchChannel.Label),
                Channel(UiMatchChannel.TestId),
            ],
            UiSmartContext.Text =>
            [
                Channel(UiMatchChannel.Text),
                Channel(UiMatchChannel.TestId),
            ],
            UiSmartContext.Section => SectionLadder,
            _ => throw new InvalidOperationException($"Unknown smart context '{context}'."),
        };

    private static IReadOnlyList<UiChannelStep> SectionLadder { get; } =
    [
        Role("region"),
        Role("group"),
        Role("form"),
        Role("table"),
        Channel(UiMatchChannel.TestId),
    ];

    private static UiChannelStep Role(string role) => new UiChannelStep(UiMatchChannel.Role, role);

    private static UiChannelStep Channel(UiMatchChannel channel) => new UiChannelStep(channel, null);
}

/// <summary>
/// One rung of a ladder: a channel, and the role it looks for when the channel is
/// <see cref="UiMatchChannel.Role"/>.
/// </summary>
/// <param name="Channel">The channel.</param>
/// <param name="Role">The ARIA role, or null.</param>
internal sealed record UiChannelStep(UiMatchChannel Channel, string? Role);
