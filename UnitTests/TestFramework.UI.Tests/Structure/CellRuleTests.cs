using System;
using TestFramework.UI.Structure;

namespace TestFramework.UI.Tests.Structure;

/// <summary>
/// The tolerance markers a test places inside its expected data.
/// </summary>
public class CellRuleTests
{
    [Fact]
    public void APlainStringIsAnExactRule()
    {
        CellRule rule = "Anvil";

        Assert.True(rule.Matches("Anvil"));
        Assert.True(rule.Matches("  Anvil  "));
        Assert.False(rule.Matches("Anvils"));
    }

    [Fact]
    public void AnyAcceptsEvenAMissingValue()
    {
        Assert.True(Cell.Any.Matches(null));
        Assert.True(Cell.Any.Matches("whatever"));
    }

    [Fact]
    public void EmptyAndNotEmptyAreOpposites()
    {
        Assert.True(Cell.Empty.Matches(null));
        Assert.True(Cell.Empty.Matches("   "));
        Assert.False(Cell.Empty.Matches("x"));

        Assert.False(Cell.NotEmpty.Matches("   "));
        Assert.True(Cell.NotEmpty.Matches("x"));
    }

    [Fact]
    public void ContainsMatchesAFragment()
    {
        Assert.True(Cell.Contains("9.00").Matches("$9.00"));
        Assert.False(Cell.Contains("ANVIL").Matches("Anvil"));
        Assert.True(Cell.Contains("ANVIL", ignoreCase: true).Matches("Anvil"));
    }

    [Fact]
    public void MatchesHandlesTextARunCannotPinDown()
    {
        CellRule rule = Cell.Matches(@"^A-\d{4}$");

        Assert.True(rule.Matches("A-1001"));
        Assert.False(rule.Matches("B-1001"));
    }

    [Fact]
    public void SatisfiesCarriesItsOwnWording()
    {
        CellRule rule = Cell.Satisfies(
            value => int.TryParse(value, out int parsed) && parsed > 0,
            "is a positive number");

        Assert.True(rule.Matches("3"));
        Assert.False(rule.Matches("0"));
        Assert.Equal("is a positive number", rule.Description);
    }

    [Fact]
    public void EveryRuleDescribesItselfForAFailureMessage()
    {
        // The description is what a reader sees when a comparison fails, so no rule may be anonymous.
        Assert.Equal("is anything", Cell.Any.Description);
        Assert.Equal("is 'Anvil'", Cell.Exactly("Anvil").Description);
        Assert.Equal("contains '9.00'", Cell.Contains("9.00").Description);
        Assert.Equal(@"matches 'A-\d+'", Cell.Matches(@"A-\d+").Description);
    }

    [Fact]
    public void ARuleWithoutADescriptionIsRefused()
        => Assert.Throws<ArgumentException>(() => Cell.Satisfies(static _ => true, "  "));
}
