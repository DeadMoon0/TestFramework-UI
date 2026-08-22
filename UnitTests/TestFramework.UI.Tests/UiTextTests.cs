namespace TestFramework.UI.Tests;

/// <summary>
/// Text normalization, which every comparison in the package goes through.
/// </summary>
public class UiTextTests
{
    [Theory]
    [InlineData("Save changes", "Save changes")]
    [InlineData("  Save changes  ", "Save changes")]
    [InlineData("Save\n   changes", "Save changes")]
    [InlineData("Save\t\tchanges", "Save changes")]
    [InlineData("Save changes", "Save changes")]
    [InlineData("   ", "")]
    [InlineData("", "")]
    public void WhitespaceCollapsesAndEndsAreTrimmed(string input, string expected)
        => Assert.Equal(expected, UiText.Normalize(input));

    [Fact]
    public void NullStaysNull()
        => Assert.Null(UiText.Normalize(null));

    [Fact]
    public void AnExactComparisonStillNormalizes()
    {
        // A page author wrapping a label across two lines is saying the same thing as the test, so
        // "exact" cannot mean "including your indentation".
        Assert.True(UiText.EqualsNormalized("  Place\n    order ", "Place order"));
    }

    [Fact]
    public void AnExactComparisonRespectsCase()
        => Assert.False(UiText.EqualsNormalized("place order", "Place order"));

    [Fact]
    public void ContainsCanIgnoreCase()
    {
        Assert.False(UiText.ContainsNormalized("Place order", "PLACE"));
        Assert.True(UiText.ContainsNormalized("Place order", "PLACE", ignoreCase: true));
    }

    [Fact]
    public void TruncationMarksWhatItCut()
    {
        Assert.Equal("abcde...", UiText.Truncate("abcdefghij", 5));
        Assert.Equal("abcde", UiText.Truncate("abcde", 5));
    }
}
