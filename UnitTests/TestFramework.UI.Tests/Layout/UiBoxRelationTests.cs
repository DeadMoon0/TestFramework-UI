using TestFramework.UI.Layout;

namespace TestFramework.UI.Tests.Layout;

/// <summary>
/// The geometry every layout check stands on: the relations, and the tolerance that keeps rounding from
/// failing them.
/// </summary>
public class UiBoxRelationTests
{
    private static UiBox Box(double x, double y, double width = 100, double height = 20)
        => new UiBox(x, y, width, height);

    [Fact]
    public void AboveMeansTheFirstEndsBeforeTheSecondBegins()
    {
        Assert.True(UiBoxRelations.IsAbove(Box(0, 0), Box(0, 30)));
        Assert.False(UiBoxRelations.IsAbove(Box(0, 30), Box(0, 0)));
    }

    [Fact]
    public void TouchingEdgesSatisfyAboveAndLeftOf()
    {
        // A form row ending exactly where the next begins is stacked, not overlapping - a relation never
        // demands a gap.
        Assert.True(UiBoxRelations.IsAbove(Box(0, 0, 100, 30), Box(0, 30)));
        Assert.True(UiBoxRelations.IsLeftOf(Box(0, 0, 50, 20), Box(50, 0)));
    }

    [Fact]
    public void ARoundingPixelDoesNotFailARelation()
    {
        // Subpixel layout leaves boxes a pixel into each other on some machines; two pixels of
        // disagreement are rounding, three are layout.
        Assert.True(UiBoxRelations.IsAbove(Box(0, 0, 100, 31), Box(0, 30)));
        Assert.False(UiBoxRelations.IsAbove(Box(0, 0, 100, 34), Box(0, 30)));
    }

    [Fact]
    public void InsideAllowsTheTolerantEdge()
    {
        UiBox outer = Box(10, 10, 100, 100);

        Assert.True(UiBoxRelations.IsInside(Box(10, 10, 100, 100), outer));
        Assert.True(UiBoxRelations.IsInside(Box(9, 9, 102, 102), outer));
        Assert.False(UiBoxRelations.IsInside(Box(5, 5, 100, 100), outer));
    }

    [Fact]
    public void OverlapNeedsBothAxesBeyondTheTolerance()
    {
        UiBox anchor = Box(0, 0, 50, 50);

        Assert.True(UiBoxRelations.Overlap(anchor, Box(40, 40, 50, 50)));

        // Sharing an edge, or grazing within the tolerance, is neighbouring rather than overlapping.
        Assert.False(UiBoxRelations.Overlap(anchor, Box(50, 0, 50, 50)));
        Assert.False(UiBoxRelations.Overlap(anchor, Box(48.5, 0, 50, 50)));

        // Overlapping in one axis only is a column, not a collision.
        Assert.False(UiBoxRelations.Overlap(anchor, Box(0, 60, 50, 50)));
    }

    [Fact]
    public void ABoxRendersTheWayADifferenceNeedsIt()
        => Assert.Equal("(24, 310, 180×48)", Box(24.3, 309.8, 180.2, 47.9).ToString());
}
