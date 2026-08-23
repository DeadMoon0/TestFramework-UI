using System;
using System.Globalization;

namespace TestFramework.UI.Layout;

/// <summary>
/// Where something is on a page: its rectangle, in the page's own pixels.
/// </summary>
/// <param name="X">The left edge.</param>
/// <param name="Y">The top edge.</param>
/// <param name="Width">How wide it is.</param>
/// <param name="Height">How tall it is.</param>
public sealed record UiBox(double X, double Y, double Width, double Height)
{
    /// <summary>The right edge.</summary>
    public double Right => this.X + this.Width;

    /// <summary>The bottom edge.</summary>
    public double Bottom => this.Y + this.Height;

    /// <summary>
    /// Renders the box the way a difference message needs it.
    /// </summary>
    /// <returns>The rendering, for example <c>(24, 310, 180×48)</c>.</returns>
    public override string ToString()
        => string.Format(
            CultureInfo.InvariantCulture,
            "({0:F0}, {1:F0}, {2:F0}×{3:F0})",
            this.X,
            this.Y,
            this.Width,
            this.Height);
}

/// <summary>
/// What one box can be said to be, relative to another.
/// </summary>
/// <remarks>
/// <para>
/// Relations, not coordinates, on purpose: "the email field is above the order button" stays true when a
/// margin changes, a font renders a pixel differently, or another machine draws the page - while "the
/// button is at y=612" is wrong the moment any of that happens. Absolute geometry is exactly the
/// brittleness the rest of the framework refuses, and layout gets no exemption.
/// </para>
/// <para>
/// Every relation carries the same tolerance: two pixels, absorbing subpixel layout and rounding without
/// forgiving anything a person could see. Edges may touch; a relation never demands a gap.
/// </para>
/// </remarks>
public static class UiBoxRelations
{
    /// <summary>How many pixels of disagreement are rounding rather than layout.</summary>
    public const double Tolerance = 2.0;

    /// <summary>Whether one box is entirely above another.</summary>
    /// <param name="above">The box that must be higher.</param>
    /// <param name="below">The box that must be lower.</param>
    /// <returns>True when the first box's bottom is at or above the second's top.</returns>
    public static bool IsAbove(UiBox above, UiBox below)
        => Check(above, below, static (a, b) => a.Bottom <= b.Y + Tolerance);

    /// <summary>Whether one box is entirely to the left of another.</summary>
    /// <param name="left">The box that must be further left.</param>
    /// <param name="right">The box that must be further right.</param>
    /// <returns>True when the first box's right edge is at or before the second's left.</returns>
    public static bool IsLeftOf(UiBox left, UiBox right)
        => Check(left, right, static (a, b) => a.Right <= b.X + Tolerance);

    /// <summary>Whether one box lies entirely within another.</summary>
    /// <param name="inner">The contained box.</param>
    /// <param name="outer">The containing box.</param>
    /// <returns>True when every edge of the inner box is within the outer one.</returns>
    public static bool IsInside(UiBox inner, UiBox outer)
        => Check(inner, outer, static (a, b) =>
            a.X >= b.X - Tolerance
            && a.Y >= b.Y - Tolerance
            && a.Right <= b.Right + Tolerance
            && a.Bottom <= b.Bottom + Tolerance);

    /// <summary>
    /// Whether two boxes overlap by more than the tolerance in both directions.
    /// </summary>
    /// <remarks>
    /// Touching is not overlapping: neighbouring cells share an edge by design, and only an overlap a
    /// person could see - both axes at once, beyond rounding - counts.
    /// </remarks>
    /// <param name="first">One box.</param>
    /// <param name="second">The other box.</param>
    /// <returns>True when they visibly overlap.</returns>
    public static bool Overlap(UiBox first, UiBox second)
        => Check(first, second, static (a, b) =>
            Math.Min(a.Right, b.Right) - Math.Max(a.X, b.X) > Tolerance
            && Math.Min(a.Bottom, b.Bottom) - Math.Max(a.Y, b.Y) > Tolerance);

    private static bool Check(UiBox first, UiBox second, Func<UiBox, UiBox, bool> relation)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        return relation(first, second);
    }
}
