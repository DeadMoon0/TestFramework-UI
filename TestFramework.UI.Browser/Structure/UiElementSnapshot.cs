using System;
using System.Collections.Generic;
using System.Linq;

namespace TestFramework.UI.Structure;

/// <summary>
/// What one element on a page actually was, as plain data.
/// </summary>
/// <remarks>
/// <para>
/// A snapshot is taken once and compared afterwards, so a comparison never races a page that is still
/// changing under it, and a failure can show the reader exactly what it was looking at.
/// </para>
/// <para>
/// It carries what a test can reason about - the element's kind, its attributes, its text - and nothing
/// it cannot: no styles, no geometry, no framework internals. That is deliberate. A structure expectation
/// that could see class names would break every time somebody restyled the page, which is the failure
/// mode that made a whole generation of snapshot tests get deleted.
/// </para>
/// </remarks>
public sealed record UiElementSnapshot
{
    internal UiElementSnapshot(string tag, IReadOnlyDictionary<string, string> attributes, string? text, IReadOnlyList<UiElementSnapshot> children)
    {
        this.Tag = tag;
        this.Attributes = attributes;
        this.Text = text;
        this.Children = children;
    }

    /// <summary>
    /// The element's kind, lower-cased - <c>div</c>, <c>button</c>, or a component's own name such as <c>app-order-row</c>.
    /// </summary>
    public string Tag { get; }

    /// <summary>
    /// The attributes it carried, by name.
    /// </summary>
    public IReadOnlyDictionary<string, string> Attributes { get; }

    /// <summary>
    /// Its text, including its descendants', normalized.
    /// </summary>
    public string? Text { get; }

    /// <summary>
    /// The elements inside it.
    /// </summary>
    public IReadOnlyList<UiElementSnapshot> Children { get; internal init; }

    /// <summary>
    /// An element with nothing in it and no text or attributes, for a test that only cares about one node.
    /// </summary>
    /// <param name="tag">The element's kind.</param>
    /// <returns>The snapshot.</returns>
    public static UiElementSnapshot Of(string tag) => Of(tag, null, null);

    /// <summary>
    /// An element with nothing in it and no attributes, for a test that only cares about one node.
    /// </summary>
    /// <param name="tag">The element's kind.</param>
    /// <param name="text">Its text.</param>
    /// <returns>The snapshot.</returns>
    public static UiElementSnapshot Of(string tag, string? text) => Of(tag, text, null);

    /// <summary>
    /// An element with nothing in it, for a test that only cares about one node.
    /// </summary>
    /// <param name="tag">The element's kind.</param>
    /// <param name="text">Its text.</param>
    /// <param name="attributes">Its attributes.</param>
    /// <returns>The snapshot.</returns>
    public static UiElementSnapshot Of(
        string tag,
        string? text,
        IReadOnlyDictionary<string, string>? attributes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);

        return new UiElementSnapshot(
            tag.ToLowerInvariant(),
            attributes ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            UiText.Normalize(text),
            Array.Empty<UiElementSnapshot>());
    }

    /// <summary>
    /// The same element with these children.
    /// </summary>
    /// <param name="children">The children.</param>
    /// <returns>A new snapshot.</returns>
    public UiElementSnapshot With(params UiElementSnapshot[] children)
        => this with { Children = children };

    /// <summary>
    /// One attribute's value, or null when the element did not carry it.
    /// </summary>
    /// <param name="name">The attribute name, matched without regard to case.</param>
    /// <returns>The value, or null.</returns>
    public string? Attribute(string name)
        => this.Attributes.TryGetValue(name, out string? value) ? value : null;

    /// <summary>
    /// How many elements this subtree contains, including this one.
    /// </summary>
    /// <returns>The count.</returns>
    public int Size() => 1 + this.Children.Sum(static child => child.Size());

    /// <summary>
    /// Renders the element as one line, for a difference message.
    /// </summary>
    /// <returns>The line.</returns>
    public override string ToString()
    {
        string attributes = this.Attributes.Count == 0
            ? string.Empty
            : " " + string.Join(" ", this.Attributes.Select(static pair => $"{pair.Key}=\"{pair.Value}\""));

        string text = string.IsNullOrEmpty(this.Text) ? string.Empty : $" \"{UiText.Truncate(this.Text, 40)}\"";

        return $"<{this.Tag}{attributes}>{text}";
    }
}
