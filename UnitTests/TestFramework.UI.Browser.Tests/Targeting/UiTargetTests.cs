using System;
using TestFramework.UI.Browser.Targeting;

namespace TestFramework.UI.Browser.Tests.Targeting;

/// <summary>
/// How a target is written, described, and rendered back as the line a reader should write.
/// </summary>
public class UiTargetTests
{
    [Fact]
    public void APlainStringIsATarget()
    {
        UiTarget target = "Save";

        Assert.Equal(UiTargetKind.Smart, target.Kind);
        Assert.Equal("Save", target.Name);
    }

    [Fact]
    public void EveryDialLeavesTheOriginalAlone()
    {
        // Targets are shared as static fields, so narrowing one in a step must not narrow it everywhere.
        UiTarget shared = Target.Button("Delete");
        UiTarget scoped = shared.InSection("Saved cards");

        Assert.Null(shared.Scope);
        Assert.NotNull(scoped.Scope);
    }

    [Theory]
    [InlineData("Save")]
    [InlineData("She said \"no\"")]
    public void APlainStringWithoutDialsRendersAsItself(string name)
        => Assert.StartsWith("\"", Target.Smart(name).ToSourceCode(), StringComparison.Ordinal);

    [Fact]
    public void ASuggestionIsAlwaysSomethingAReaderCanPaste()
    {
        // The whole point of putting code in a failure message is that it can be copied. A bare string
        // with a method chained onto it would not compile, so a dialled smart target names its factory.
        Assert.Equal(
            "Target.Smart(\"Delete\").InSection(\"Saved cards\")",
            Target.Smart("Delete").InSection("Saved cards").ToSourceCode());

        Assert.Equal("Target.Smart(\"Delete\").First()", Target.Smart("Delete").First().ToSourceCode());
        Assert.Equal("Target.Smart(\"Delete\").Nth(1)", Target.Smart("Delete").Nth(1).ToSourceCode());
        Assert.Equal("Target.Smart(\"Delete\").ExactMatch()", Target.Smart("Delete").ExactMatch().ToSourceCode());
    }

    [Fact]
    public void ATypedTargetRendersAsItsFactory()
    {
        Assert.Equal("Target.Button(\"Save\")", Target.Button("Save").ToSourceCode());
        Assert.Equal("Target.TestId(\"submit\")", Target.TestId("submit").ToSourceCode());
        Assert.Equal("Target.Css(\"#legacy\")", Target.Css("#legacy").ToSourceCode());
        Assert.Equal("Target.Role(\"tab\", \"Details\")", Target.Role("tab", "Details").ToSourceCode());
    }

    [Fact]
    public void ADescriptionReadsAsProse()
    {
        Assert.Equal("button 'Save'", Target.Button("Save").Describe());
        Assert.Equal(
            "button 'Delete' in section 'Saved cards'",
            Target.Button("Delete").InSection("Saved cards").Describe());
        Assert.Equal("field 'Email' near 'Billing'", Target.Field("Email").Near("Billing").Describe());
    }

    [Fact]
    public void ATargetRefusesToBeNameless()
    {
        Assert.Throws<ArgumentException>(() => Target.Button("  "));
        Assert.Throws<ArgumentException>(() => Target.Css(string.Empty));
    }
}
