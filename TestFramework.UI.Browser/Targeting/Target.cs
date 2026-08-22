using System;

namespace TestFramework.UI.Browser.Targeting;

/// <summary>
/// Names the element a step should act on.
/// </summary>
/// <remarks>
/// Most steps never need this class: a plain string already names an element, and the verb decides
/// what kind of element it means. Reach for these factories when a page needs the extra precision -
/// when the kind matters, when the same name occurs twice, or when nothing user-facing identifies the
/// element and a selector is the honest answer.
/// </remarks>
public static class Target
{
    /// <summary>
    /// An element named this, with the kind decided by the verb: something clickable for <c>Click</c>,
    /// something fillable for <c>Fill</c>, any text for <c>Expect</c>.
    /// </summary>
    /// <param name="name">The name, label or text of the element.</param>
    /// <returns>The target.</returns>
    public static UiTarget Smart(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new UiTarget(UiTargetKind.Smart, name);
    }

    /// <summary>
    /// A button with this accessible name. Pins the kind, so a button silently becoming a link fails
    /// the test rather than passing quietly.
    /// </summary>
    /// <param name="name">The button's accessible name.</param>
    /// <returns>The target.</returns>
    public static UiTarget Button(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new UiTarget(UiTargetKind.Button, name);
    }

    /// <summary>A link with this accessible name.</summary>
    /// <param name="name">The link's accessible name.</param>
    /// <returns>The target.</returns>
    public static UiTarget Link(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new UiTarget(UiTargetKind.Link, name);
    }

    /// <summary>
    /// A form control identified by its label, or failing that its placeholder or accessible name.
    /// </summary>
    /// <param name="label">The control's label.</param>
    /// <returns>The target.</returns>
    public static UiTarget Field(string label)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);

        return new UiTarget(UiTargetKind.Field, label);
    }

    /// <summary>A checkbox identified by its label.</summary>
    /// <param name="label">The checkbox's label.</param>
    /// <returns>The target.</returns>
    public static UiTarget Checkbox(string label)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);

        return new UiTarget(UiTargetKind.Checkbox, label);
    }

    /// <summary>A radio button identified by its label.</summary>
    /// <param name="label">The radio button's label.</param>
    /// <returns>The target.</returns>
    public static UiTarget Radio(string label)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);

        return new UiTarget(UiTargetKind.Radio, label);
    }

    /// <summary>Any element carrying this visible text.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The target.</returns>
    public static UiTarget Text(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        return new UiTarget(UiTargetKind.Text, text);
    }

    /// <summary>
    /// An element with this ARIA role, optionally narrowed by accessible name. Use it for roles the
    /// other factories do not cover - <c>tab</c>, <c>dialog</c>, <c>alert</c>, <c>option</c>.
    /// </summary>
    /// <param name="role">The ARIA role.</param>
    /// <param name="name">The accessible name, or null for any element of that role.</param>
    /// <returns>The target.</returns>
    public static UiTarget Role(string role, string? name = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(role);

        return new UiTarget(UiTargetKind.Role, name, role);
    }

    /// <summary>
    /// A region, group or form with this accessible name. Mostly used through
    /// <see cref="UiTarget.InSection"/> to scope a search.
    /// </summary>
    /// <param name="name">The section's accessible name.</param>
    /// <returns>The target.</returns>
    public static UiTarget Section(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new UiTarget(UiTargetKind.Section, name);
    }

    /// <summary>
    /// An element carrying this value in the configured test id attribute (<c>data-testid</c> unless
    /// configured otherwise). The right answer when the visible text is generated or translated.
    /// </summary>
    /// <param name="id">The test id.</param>
    /// <returns>The target.</returns>
    public static UiTarget TestId(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        return new UiTarget(UiTargetKind.TestId, id);
    }

    /// <summary>
    /// An element matched by CSS selector.
    /// </summary>
    /// <remarks>
    /// This is the one channel that ties a test to the document's shape rather than to what a user
    /// perceives, so a restyle or a refactor can break it. It is here because sometimes nothing
    /// user-facing identifies the element, and an honest selector beats a fragile guess.
    /// </remarks>
    /// <param name="selector">The CSS selector.</param>
    /// <returns>The target.</returns>
    public static UiTarget Css(string selector)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(selector);

        return new UiTarget(UiTargetKind.Css, null, null, selector);
    }

    /// <summary>An element with this <c>id</c> attribute.</summary>
    /// <param name="elementId">The id attribute value.</param>
    /// <returns>The target.</returns>
    public static UiTarget Id(string elementId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(elementId);

        return new UiTarget(UiTargetKind.ElementId, elementId);
    }
}
