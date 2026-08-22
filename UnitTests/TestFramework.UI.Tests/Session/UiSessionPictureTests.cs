using System;
using System.Collections.Generic;
using TestFramework.UI.Session;

namespace TestFramework.UI.Tests.Session;

/// <summary>
/// The picture a run paints as it goes, and the audit a suite can run over it.
/// </summary>
public class UiSessionPictureTests
{
    private static UiSessionEntry Entry(
        string step,
        string action,
        string? target = null,
        string? via = null,
        int? rank = null,
        int candidates = 1,
        IReadOnlyList<string>? consoleErrors = null)
        => new UiSessionEntry(
            step,
            action,
            target,
            via,
            rank,
            candidates,
            MatchedSnippet: null,
            Detail: null,
            Url: "http://localhost/checkout",
            ConsoleErrors: consoleErrors ?? Array.Empty<string>(),
            DurationMs: 12);

    [Fact]
    public void APictureGrowsWithoutChangingWhatCameBefore()
    {
        UiSessionPicture empty = UiSessionPicture.Empty("shop");
        UiSessionPicture after = empty.Add([Entry("checkout", "Click", "button 'Pay'")], "http://localhost/done", "Done");

        Assert.Empty(empty.Entries);
        Assert.Single(after.Entries);
        Assert.Equal("http://localhost/done", after.Url);
        Assert.Equal("Done", after.Title);
    }

    [Fact]
    public void EntriesCanBeReadBackPerStep()
    {
        UiSessionPicture picture = UiSessionPicture.Empty("shop")
            .Add([Entry("cart", "Click")], "http://localhost/cart", "Cart")
            .Add([Entry("checkout", "Fill"), Entry("checkout", "Click")], "http://localhost/done", "Done");

        Assert.Single(picture.For("cart"));
        Assert.Equal(2, picture.For("checkout").Count);
        Assert.Empty(picture.For("nothing-by-that-name"));
    }

    [Fact]
    public void ASessionThatOnlyMatchedExactlyReportsNoWeakness()
    {
        UiSessionPicture picture = UiSessionPicture.Empty("shop")
            .Add([Entry("checkout", "Click", "button 'Pay'", "RoleExact", rank: 0)], "http://localhost", "Shop");

        Assert.Null(picture.WeakestMatch());
        Assert.Empty(picture.LooseMatches());
    }

    [Fact]
    public void LooseMatchesAreTheAuditPrimitive()
    {
        // What a suite asserts on to keep tolerance honest: empty means every element was found by
        // being what it claimed to be, without the test needing to know which channel matched.
        UiSessionPicture picture = UiSessionPicture.Empty("shop")
            .Add(
            [
                Entry("checkout", "Click", "button 'Pay'", "RoleExact", rank: 0),
                Entry("checkout", "Fill", "field 'Email'", "PlaceholderLoose", rank: 4),
                Entry("checkout", "Click", "button 'Delete'", "RoleExact", rank: 0, candidates: 3),
            ],
            "http://localhost",
            "Shop");

        Assert.Equal(2, picture.LooseMatches().Count);
    }

    [Fact]
    public void TheWeakestMatchIsWhatAnAuditReportsOn()
    {
        // The point of the audit: a suite can stay forgiving while tests are written, and still refuse
        // to go green on tests that only pass because the framework guessed well.
        UiSessionPicture picture = UiSessionPicture.Empty("shop")
            .Add(
            [
                Entry("checkout", "Click", "button 'Pay'", "RoleExact", rank: 0),
                Entry("checkout", "Fill", "field 'Email'", "PlaceholderLoose", rank: 4),
                Entry("checkout", "Click", "button 'Next'", "RoleLoose", rank: 2),
            ],
            "http://localhost",
            "Shop");

        UiSessionEntry? weakest = picture.WeakestMatch();

        Assert.NotNull(weakest);
        Assert.Equal("PlaceholderLoose", weakest.ResolvedVia);
        Assert.True(weakest.IsLooseMatch);
    }

    [Fact]
    public void ChoosingAmongCandidatesCountsAsLooseEvenAtTheStrongestChannel()
    {
        UiSessionEntry entry = Entry("checkout", "Click", "button 'Delete'", "RoleExact", rank: 0, candidates: 3);

        Assert.True(entry.IsLooseMatch, "the run picked one of three, which an audit has to be able to see");
    }

    [Fact]
    public void ConsoleErrorsAreCollectedAcrossTheWholeSession()
    {
        UiSessionPicture picture = UiSessionPicture.Empty("shop")
            .Add([Entry("cart", "Click", consoleErrors: ["TypeError: x is undefined"])], "http://localhost", "Shop")
            .Add([Entry("checkout", "Click")], "http://localhost", "Shop");

        Assert.Single(picture.ConsoleErrors());
    }

    [Fact]
    public void APictureRendersAsNumberedLines()
    {
        UiSessionPicture picture = UiSessionPicture.Empty("shop")
            .Add([Entry("checkout", "Click", "button 'Pay'", "RoleExact", rank: 0)], "http://localhost/done", "Done");

        string rendered = picture.ToString();

        Assert.Contains("1. Click button 'Pay' via RoleExact", rendered, StringComparison.Ordinal);
        Assert.Contains("http://localhost/done", rendered, StringComparison.Ordinal);
    }

    [Fact]
    public void SessionVariablesAreRecognisableByName()
    {
        Assert.Equal("ui:shop", UiSessionVariable.For("shop"));
        Assert.True(UiSessionVariable.IsSessionVariable("ui:shop"));
        Assert.False(UiSessionVariable.IsSessionVariable("orderNo"));
    }
}
