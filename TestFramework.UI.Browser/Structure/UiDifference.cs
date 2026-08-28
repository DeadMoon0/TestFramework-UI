namespace TestFramework.UI.Structure;

/// <summary>
/// What kind of difference a comparison found.
/// </summary>
/// <remarks>
/// The words come from the oldest tool that got this right: an expected row that is not there is
/// <em>missing</em>, an actual row nobody expected is <em>surplus</em>. They have been the clearest way to
/// report a set comparison for decades, and inventing new ones would only make failures harder to read.
/// </remarks>
public enum UiDifferenceKind
{
    /// <summary>The page does not have something the test expected.</summary>
    Missing = 0,

    /// <summary>The page has something the test did not expect, where the test said it should not.</summary>
    Surplus,

    /// <summary>The element is there, but something about it is not what was expected.</summary>
    RuleFailed,

    /// <summary>Everything is there, but not in the order the test asked for.</summary>
    OutOfOrder,
}

/// <summary>
/// One way the page differed from what a test expected, and where.
/// </summary>
/// <param name="Kind">What kind of difference it is.</param>
/// <param name="Path">Where it is, as a path through the structure, for example
/// <c>app-order-list &gt; app-order-row</c>.</param>
/// <param name="Detail">What exactly differed, in the words of the expectation that failed.</param>
public sealed record UiDifference(UiDifferenceKind Kind, string Path, string Detail)
{
    /// <summary>
    /// Renders the difference as one line: where it is, then what it is.
    /// </summary>
    /// <remarks>
    /// The path comes first because a reader scanning a list of differences is looking for <em>where</em>
    /// before <em>what</em> - the same reason a compiler puts the file and line before the message.
    /// </remarks>
    /// <returns>The line.</returns>
    public override string ToString() => $"{this.Path}: {this.Detail}";
}
