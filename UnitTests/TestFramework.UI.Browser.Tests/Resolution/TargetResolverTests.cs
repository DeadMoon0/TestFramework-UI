using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.UI.Browser.Exceptions;
using TestFramework.UI.Browser.Resolution;
using TestFramework.UI.Browser.Targeting;
using TestFramework.UI.Browser.Tests.Fakes;

namespace TestFramework.UI.Browser.Tests.Resolution;

/// <summary>
/// The matching ladder, which is what a test relies on when it says <c>Click("Save")</c> and the page
/// has meanwhile been restyled, relocated or renamed.
/// </summary>
public class TargetResolverTests
{
    private static Task<UiResolvedTarget> Resolve(
        FakePage page,
        UiTarget target,
        UiSmartContext context = UiSmartContext.Clickable,
        UiResolutionOptions? options = null)
        => TargetResolver.ResolveAsync(
            page,
            target,
            context,
            options ?? UiResolutionOptions.Default,
            "shop",
            "http://localhost/checkout",
            CancellationToken.None);

    [Fact]
    public async Task AnExactMatchOnAWeakChannelBeatsALooseMatchOnAStrongOne()
    {
        // The reason matching runs in two sweeps rather than exact-then-loose per channel. Buttons are
        // a stronger channel than links, but the link is called exactly what the test asked for, and
        // certainty must win over a plausible guess.
        FakePage page = new FakePage(
            new FakeElement("button", Role: "button", Name: "Save changes"),
            new FakeElement("a", Role: "link", Name: "Save"));

        UiResolvedTarget resolved = await Resolve(page, "Save");

        Assert.Equal("link", resolved.Spec.Role);
        Assert.True(resolved.Spec.Exact);
        Assert.Equal("RoleExact", resolved.DescribeMatch());
    }

    [Fact]
    public async Task ARenamedControlIsStillFoundAndTheLooseMatchIsRecorded()
    {
        FakePage page = new FakePage(new FakeElement("button", Role: "button", Name: "Save changes"));

        UiResolvedTarget resolved = await Resolve(page, "Save");

        Assert.Equal("RoleLoose", resolved.DescribeMatch());
        Assert.True(resolved.Rank > 0, "a loose match must not report the strongest rank");
        Assert.True(resolved.IsLoose, "the run guessed, so a resilience audit has to be able to see it");
    }

    [Fact]
    public async Task ExactMatchRefusesToGuess()
    {
        FakePage page = new FakePage(new FakeElement("button", Role: "button", Name: "Save changes"));

        await Assert.ThrowsAsync<UiTargetNotFoundException>(
            () => Resolve(page, Target.Smart("Save").ExactMatch()));
    }

    [Fact]
    public async Task WhitespaceAndWrappingDoNotBreakAnExactMatch()
    {
        FakePage page = new FakePage(new FakeElement("button", Role: "button", Name: "  Save\n   changes  "));

        UiResolvedTarget resolved = await Resolve(page, Target.Smart("Save changes").ExactMatch());

        Assert.Equal("RoleExact", resolved.DescribeMatch());
    }

    [Fact]
    public async Task TheLooseSweepIgnoresCase()
    {
        FakePage page = new FakePage(new FakeElement("button", Role: "button", Name: "Save"));

        UiResolvedTarget resolved = await Resolve(page, "save");

        Assert.Equal("RoleLoose", resolved.DescribeMatch());
    }

    [Fact]
    public async Task MovingAControlChangesNothing()
    {
        // Position is never part of a target, so there is nothing here for a layout change to break.
        FakePage moved = new FakePage(
            new FakeElement("div", Text: "Footer"),
            new FakeElement("nav", Text: "Menu"),
            new FakeElement("button", Role: "button", Name: "Add to cart", Section: "Recommended"));

        UiResolvedTarget resolved = await Resolve(moved, "Add to cart");

        Assert.Equal("RoleExact", resolved.DescribeMatch());
        Assert.Equal(0, resolved.Rank);
    }

    [Fact]
    public async Task AmbiguityFailsAndTheMessageCarriesTheWayOut()
    {
        FakePage page = new FakePage(
            new FakeElement("button", Role: "button", Name: "Delete", Section: "Saved cards"),
            new FakeElement("button", Role: "button", Name: "Delete", Section: "Addresses"),
            new FakeElement("button", Role: "button", Name: "Delete", Section: "Account", TestId: "delete-account"));

        UiAmbiguousTargetException exception = await Assert.ThrowsAsync<UiAmbiguousTargetException>(
            () => Resolve(page, Target.Button("Delete")));

        Assert.Equal(3, exception.MatchCount);
        Assert.Equal(3, exception.Candidates.Count);

        // Every candidate is named, so the reader never has to open the page to tell them apart.
        Assert.Contains("Saved cards", exception.Message, System.StringComparison.Ordinal);
        Assert.Contains("Addresses", exception.Message, System.StringComparison.Ordinal);

        // And the message hands over the next line to write, not just the diagnosis.
        Assert.Contains("Target.Button(\"Delete\").InSection(\"Saved cards\")", exception.Message, System.StringComparison.Ordinal);
        Assert.Contains("Target.TestId(\"delete-account\")", exception.Message, System.StringComparison.Ordinal);
        Assert.Contains(".First()", exception.Message, System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task ScopingToASectionResolvesAmbiguity()
    {
        FakePage page = new FakePage(
            new FakeElement("section", Role: "region", Name: "Saved cards"),
            new FakeElement("section", Role: "region", Name: "Addresses"),
            new FakeElement("button", Role: "button", Name: "Delete", Section: "Saved cards"),
            new FakeElement("button", Role: "button", Name: "Delete", Section: "Addresses"));

        UiResolvedTarget resolved = await Resolve(page, Target.Button("Delete").InSection("Saved cards"));

        Assert.Equal(1, resolved.CandidateCount);
        Assert.NotNull(resolved.Spec.Within);
    }

    [Fact]
    public async Task AnAmbiguousSectionFailsRatherThanScopingToAGuess()
    {
        FakePage page = new FakePage(
            new FakeElement("section", Role: "region", Name: "Cards"),
            new FakeElement("section", Role: "region", Name: "Cards"),
            new FakeElement("button", Role: "button", Name: "Delete", Section: "Cards"));

        UiAmbiguousTargetException exception = await Assert.ThrowsAsync<UiAmbiguousTargetException>(
            () => Resolve(page, Target.Button("Delete").InSection("Cards")));

        Assert.Contains("section 'Cards'", exception.TargetDescription, System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task FirstOptsIntoAcceptingOneOfSeveral()
    {
        FakePage page = new FakePage(
            new FakeElement("button", Role: "button", Name: "Delete"),
            new FakeElement("button", Role: "button", Name: "Delete"));

        UiResolvedTarget resolved = await Resolve(page, Target.Button("Delete").First());

        Assert.Equal(0, resolved.Index);
        Assert.Equal(2, resolved.CandidateCount);
        Assert.True(resolved.IsLoose, "choosing among candidates is exactly what an audit should see");
    }

    [Fact]
    public async Task TheLenientModeAppliesWithoutTouchingTheTest()
    {
        FakePage page = new FakePage(
            new FakeElement("button", Role: "button", Name: "Delete"),
            new FakeElement("button", Role: "button", Name: "Delete"));

        UiResolvedTarget resolved = await Resolve(
            page,
            Target.Button("Delete"),
            options: new UiResolutionOptions(UiAmbiguityMode.FirstMatch));

        Assert.Equal(0, resolved.Index);
        Assert.Equal(2, resolved.CandidateCount);
    }

    [Fact]
    public async Task NthPicksAMatchByPosition()
    {
        FakePage page = new FakePage(
            new FakeElement("button", Role: "button", Name: "Delete", Section: "First"),
            new FakeElement("button", Role: "button", Name: "Delete", Section: "Second"));

        UiResolvedTarget resolved = await Resolve(page, Target.Button("Delete").Nth(1));

        Assert.Equal(1, resolved.Index);
    }

    [Fact]
    public async Task NthBeyondTheMatchesFailsInsteadOfActingOnNothing()
    {
        FakePage page = new FakePage(
            new FakeElement("button", Role: "button", Name: "Delete"),
            new FakeElement("button", Role: "button", Name: "Delete"));

        await Assert.ThrowsAsync<UiAmbiguousTargetException>(() => Resolve(page, Target.Button("Delete").Nth(5)));
    }

    [Fact]
    public async Task ANotFoundFailureNamesWhatThePageDoesOffer()
    {
        FakePage page = new FakePage(
            new FakeElement("button", Role: "button", Name: "Save changes"),
            new FakeElement("button", Role: "button", Name: "Cancel"));

        UiTargetNotFoundException exception = await Assert.ThrowsAsync<UiTargetNotFoundException>(
            () => Resolve(page, Target.Smart("Submit").ExactMatch()));

        Assert.Contains("'Save changes'", exception.Message, System.StringComparison.Ordinal);
        Assert.Contains("'Cancel'", exception.Message, System.StringComparison.Ordinal);
        Assert.NotEmpty(exception.Tried);
    }

    [Fact]
    public async Task ARenameIsReportedAsTheLineThatWouldHaveWorked()
    {
        FakePage page = new FakePage(new FakeElement("button", Role: "button", Name: "Save changes"));

        UiTargetNotFoundException exception = await Assert.ThrowsAsync<UiTargetNotFoundException>(
            () => Resolve(page, Target.Button("Save").ExactMatch()));

        Assert.Equal("Save changes", exception.ClosestName);
        Assert.Contains("Did you mean: Target.Button(\"Save changes\")", exception.Message, System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task AWildlyWrongNameIsNotGivenAMisleadingSuggestion()
    {
        FakePage page = new FakePage(new FakeElement("button", Role: "button", Name: "Cancel"));

        UiTargetNotFoundException exception = await Assert.ThrowsAsync<UiTargetNotFoundException>(
            () => Resolve(page, Target.Button("Place order")));

        Assert.Null(exception.ClosestName);
    }

    [Fact]
    public async Task AnEmptyPageSaysSoRatherThanSuggestingNothing()
    {
        FakePage page = new FakePage();

        UiTargetNotFoundException exception = await Assert.ThrowsAsync<UiTargetNotFoundException>(
            () => Resolve(page, "Save"));

        Assert.Contains("no element of that kind at all", exception.Message, System.StringComparison.Ordinal);
        Assert.Contains("WaitForEvent", exception.Message, System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task NamingTheKindKeepsTheTestHonestAboutIt()
    {
        // The page turned a button into a link. A test that asked for a button should say so: the two
        // behave differently for keyboard and screen-reader users.
        FakePage page = new FakePage(new FakeElement("a", Role: "link", Name: "Save"));

        await Assert.ThrowsAsync<UiTargetNotFoundException>(() => Resolve(page, Target.Button("Save")));

        // While the forgiving form finds it, because it only ever asked for something pressable.
        UiResolvedTarget resolved = await Resolve(page, "Save");
        Assert.Equal("link", resolved.Spec.Role);
    }

    [Fact]
    public async Task ALabelledFieldWinsOverAPlaceholderSayingTheSameThing()
    {
        FakePage page = new FakePage(
            new FakeElement("input", Placeholder: "Email"),
            new FakeElement("input", Label: "Email"));

        UiResolvedTarget resolved = await Resolve(page, "Email", UiSmartContext.Fillable);

        Assert.Equal(UiMatchChannel.Label, resolved.Spec.Channel);
    }

    [Fact]
    public async Task APlaceholderIsUsedWhenNoLabelExists()
    {
        FakePage page = new FakePage(new FakeElement("input", Placeholder: "Email"));

        UiResolvedTarget resolved = await Resolve(page, "Email", UiSmartContext.Fillable);

        Assert.Equal(UiMatchChannel.Placeholder, resolved.Spec.Channel);
        Assert.True(resolved.Rank > 0, "a hint is weaker than a label, and the rank should say so");
    }

    [Fact]
    public async Task ATestIdIsNeverTriedLoosely()
    {
        // A partial test id is not a weaker way of saying the same thing - it is a different id. Trying
        // it loosely would invent matches and inflate every rank behind it.
        FakePage page = new FakePage(new FakeElement("button", TestId: "submit-order"));

        UiResolvedTarget resolved = await Resolve(page, Target.TestId("submit-order"));

        Assert.Equal("TestId", resolved.DescribeMatch());
        Assert.All(page.Queries, spec => Assert.True(
            spec.Channel != UiMatchChannel.TestId || spec.Exact,
            "the test id channel must only ever be queried exactly"));
    }

    [Fact]
    public async Task ASelectorIsUsedExactlyAsGiven()
    {
        FakePage page = new FakePage(
            new FakeElement("button", Selectors: ["#legacy-submit"]));

        UiResolvedTarget resolved = await Resolve(page, Target.Css("#legacy-submit"));

        Assert.Equal(UiMatchChannel.Css, resolved.Spec.Channel);
        Assert.Equal("#legacy-submit", resolved.Spec.Css);
        Assert.Equal(0, resolved.Rank);
    }

    [Fact]
    public async Task TheStrongestChannelIsAlwaysTriedFirst()
    {
        FakePage page = new FakePage(new FakeElement("button", Role: "button", Name: "Save"));

        await Resolve(page, "Save");

        UiQuerySpec first = page.Queries[0];
        Assert.Equal(UiMatchChannel.Role, first.Channel);
        Assert.Equal("button", first.Role);
        Assert.True(first.Exact);
    }

    [Fact]
    public async Task EveryExactChannelIsTriedBeforeAnyLooseOne()
    {
        FakePage page = new FakePage(new FakeElement("div", Text: "Thank you"));

        await Resolve(page, "Thank you", UiSmartContext.Clickable);

        int lastExact = page.Queries.FindLastIndex(static spec => spec.Exact && !UiChannelLadder.IsExactOnly(spec.Channel));
        int firstLoose = page.Queries.FindIndex(static spec => !spec.Exact);

        Assert.True(firstLoose == -1 || lastExact < firstLoose, "the exact sweep must complete before the loose one starts");
    }

    [Fact]
    public async Task NearNarrowsToTheRightNeighbourhood()
    {
        FakePage page = new FakePage(
            new FakeElement("button", Role: "button", Name: "Edit", Section: "Shipping address"),
            new FakeElement("button", Role: "button", Name: "Edit", Section: "Billing address"));

        UiResolvedTarget resolved = await Resolve(page, Target.Button("Edit").Near("Billing"));

        Assert.Equal(1, resolved.CandidateCount);
        Assert.Equal("Billing", resolved.Spec.NearText);
    }
}
