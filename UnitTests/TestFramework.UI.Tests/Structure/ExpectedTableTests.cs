using System;
using System.Collections.Generic;
using System.Linq;
using TestFramework.UI.Structure;

namespace TestFramework.UI.Tests.Structure;

/// <summary>
/// The row-set algebra: unordered by key, missing and surplus, per-cell tolerance.
/// </summary>
public class ExpectedTableTests
{
    private static UiTableSnapshot Table(params string[][] rows)
        => new UiTableSnapshot(
            ["Order", "Product", "Qty", "Price"],
            rows.Select(static row => (IReadOnlyList<string>)row).ToList());

    private static readonly string[] Anvil = ["A-1001", "Anvil", "1", "€129.00"];
    private static readonly string[] Rope = ["A-1002", "Rope", "3", "€9.00"];
    private static readonly string[] Crate = ["A-1003", "Crate", "2", "€24.50"];

    [Fact]
    public void ATableThatMatchesReportsNothing()
    {
        ExpectedTable expected = ExpectedTable.WithHeader("Order", "Product", "Qty")
            .Row("A-1001", "Anvil", "1")
            .Row("A-1002", "Rope", "3")
            .AllowExtraRows();

        Assert.Empty(expected.Compare(Table(Anvil, Rope, Crate)));
    }

    [Fact]
    public void SortingDifferentlyIsNotADifference()
    {
        // A table sorted the other way holds the same data, and a test about the data should not care.
        ExpectedTable expected = ExpectedTable.WithHeader("Order", "Product")
            .Row("A-1001", "Anvil")
            .Row("A-1002", "Rope");

        Assert.Empty(expected.Compare(new UiTableSnapshot(
            ["Order", "Product"],
            [["A-1002", "Rope"], ["A-1001", "Anvil"]])));
    }

    [Fact]
    public void OrderCanBeRequiredWhenItIsThePoint()
    {
        ExpectedTable expected = ExpectedTable.WithHeader("Order", "Product")
            .Row("A-1001", "Anvil")
            .Row("A-1002", "Rope")
            .InOrder();

        IReadOnlyList<UiDifference> differences = expected.Compare(new UiTableSnapshot(
            ["Order", "Product"],
            [["A-1002", "Rope"], ["A-1001", "Anvil"]]));

        Assert.Equal(UiDifferenceKind.OutOfOrder, differences.Single().Kind);
    }

    [Fact]
    public void ColumnsNobodyMentionedAreNotCompared()
    {
        // A table gaining a column must not break every test that reads it.
        ExpectedTable expected = ExpectedTable.WithHeader("Order").Row("A-1001").AllowExtraRows();

        Assert.Empty(expected.Compare(Table(Anvil)));
    }

    [Fact]
    public void AColumnTheTableDoesNotHaveIsReportedOnceRatherThanPerRow()
    {
        ExpectedTable expected = ExpectedTable.WithHeader("Order", "Discount")
            .Row("A-1001", "10%")
            .Row("A-1002", "5%");

        IReadOnlyList<UiDifference> differences = expected.Compare(Table(Anvil, Rope));

        UiDifference difference = Assert.Single(differences);
        Assert.Equal("column 'Discount'", difference.Path);
        Assert.Contains("'Order'", difference.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void AnExpectedRowThatIsNotThereIsMissing()
    {
        ExpectedTable expected = ExpectedTable.WithHeader("Order", "Product")
            .Row("A-9999", "Ghost")
            .AllowExtraRows();

        UiDifference difference = expected.Compare(Table(Anvil)).Single();

        Assert.Equal(UiDifferenceKind.Missing, difference.Kind);
        Assert.Contains("no row where 'Order' is 'A-9999'", difference.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void ARowNobodyExpectedIsSurplus()
    {
        ExpectedTable expected = ExpectedTable.WithHeader("Order", "Product").Row("A-1001", "Anvil");

        IReadOnlyList<UiDifference> differences = expected.Compare(Table(Anvil, Rope));

        UiDifference difference = differences.Single();
        Assert.Equal(UiDifferenceKind.Surplus, difference.Kind);
        Assert.Contains("A-1002", difference.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void AMatchedRowWithAWrongCellPointsAtTheCell()
    {
        // The difference between "a row is missing" and "the price is wrong" is the whole value of keying on
        // something identifying.
        ExpectedTable expected = ExpectedTable.WithHeader("Order", "Product", "Price")
            .Row("A-1001", "Anvil", "€99.00")
            .AllowExtraRows();

        UiDifference difference = expected.Compare(Table(Anvil)).Single();

        Assert.Equal(UiDifferenceKind.RuleFailed, difference.Kind);
        Assert.Equal("row 1, column 'Price'", difference.Path);
        Assert.Contains("expected is '€99.00', found '€129.00'", difference.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void TheKeyColumnCanBeNamed()
    {
        ExpectedTable expected = ExpectedTable.WithHeader("Order", "Product", "Qty")
            .Row(Cell.Any, "Rope", "3")
            .KeyedBy("Product")
            .AllowExtraRows();

        Assert.Empty(expected.Compare(Table(Anvil, Rope)));
    }

    [Fact]
    public void CellRulesCarryTheTolerance()
    {
        ExpectedTable expected = ExpectedTable.WithHeader("Order", "Product", "Price")
            .Row(Cell.Matches(@"^A-\d{4}$"), "Anvil", Cell.Contains("129"))
            .Row(Cell.Any, "Rope", Cell.NotEmpty)
            .AllowExtraRows();

        Assert.Empty(expected.Compare(Table(Anvil, Rope)));
    }

    [Fact]
    public void ARowThatDoesNotMatchTheHeaderIsRefusedWhileItIsBeingWritten()
    {
        // Caught when the test is written rather than when it runs: a row with the wrong number of cells is
        // a typo, and a typo should not need a browser to be discovered.
        ArgumentException failure = Assert.Throws<ArgumentException>(
            () => ExpectedTable.WithHeader("Order", "Product").Row("A-1001"));

        Assert.Contains("Cell.Any", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void KeyingOnAColumnTheTableDoesNotNameIsRefused()
        => Assert.Throws<ArgumentException>(() => ExpectedTable.WithHeader("Order").KeyedBy("Nope"));

    [Fact]
    public void CellsAreComparedNormalized()
        => Assert.Empty(ExpectedTable.WithHeader("Order").Row("A-1001").Compare(
            new UiTableSnapshot(["Order"], [["  A-1001\n "]])));

    [Fact]
    public void ATableFreezesOnFirstUse()
    {
        // Like its two structure siblings: a table shared by several timelines must not be editable by
        // one of them after another has started comparing against it, or what the frozen run proved
        // would change with no write ever throwing.
        ExpectedTable expected = ExpectedTable.WithHeader("Order").Row("A-1001");

        expected.Compare(new UiTableSnapshot(["Order"], [["A-1001"]]));

        Assert.Throws<InvalidOperationException>(() => expected.Row("A-1002"));
        Assert.Throws<InvalidOperationException>(() => expected.AllowExtraRows());
        Assert.Throws<InvalidOperationException>(() => expected.InOrder());
        Assert.Throws<InvalidOperationException>(() => expected.KeyedBy("Order"));
    }
}
