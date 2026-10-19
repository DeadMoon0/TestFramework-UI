using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.UI.Browser.Exceptions;
using TestFramework.UI.Browser.Resolution;
using TestFramework.UI.Browser.Targeting;
using TestFramework.UI.Browser.Tests.Fakes;

namespace TestFramework.UI.Browser.Tests.Resolution;

/// <summary>
/// What happens when the page changes while it is being searched.
/// </summary>
/// <remarks>
/// Every channel is a separate round trip to the browser, so a page that finishes rendering halfway
/// through a search can answer "nothing here" to the strong channels and "here it is" to a weak one. That
/// is a fact about timing, not about the page, and reporting it as a loose match would both mislead a
/// reader and quietly downgrade what an audit is measuring. Found by measuring a real Angular page, not
/// by reasoning about one.
/// </remarks>
public class MidRenderTests
{
    /// <summary>
    /// A page that answers nothing until it has been asked a few times - a form still rendering.
    /// </summary>
    private sealed class LateRenderingPage : IUiElementQuery
    {
        private readonly FakePage page;
        private readonly int silentAnswers;
        private int answers;

        public LateRenderingPage(int silentAnswers, params FakeElement[] elements)
        {
            this.silentAnswers = silentAnswers;
            this.page = new FakePage(elements);
        }

        public async Task<int> CountAsync(UiQuerySpec spec, CancellationToken cancellationToken)
        {
            this.answers++;

            return this.answers <= this.silentAnswers
                ? 0
                : await this.page.CountAsync(spec, cancellationToken);
        }

        public Task<IReadOnlyList<UiCandidate>> DescribeAsync(UiQuerySpec spec, int max, CancellationToken cancellationToken)
            => this.page.DescribeAsync(spec, max, cancellationToken);

        public Task<IReadOnlyList<string>> AvailableNamesAsync(UiQuerySpec spec, int max, CancellationToken cancellationToken)
            => this.page.AvailableNamesAsync(spec, max, cancellationToken);
    }

    [Fact]
    public async Task APageThatRendersMidSearchStillReportsTheExactMatch()
    {
        // The label channel is asked first and answers nothing because the form is not there yet. By the
        // time the loose sweep runs, it is. Without a second look the run would report a fuzzy match on a
        // page that names the field exactly.
        LateRenderingPage page = new LateRenderingPage(
            silentAnswers: 6,
            new FakeElement("input", Label: "Email"));

        UiResolvedTarget resolved = await TargetResolver.ResolveAsync(
            page,
            "Email",
            UiSmartContext.Fillable,
            UiResolutionOptions.Default,
            "shop",
            "http://localhost/checkout",
            CancellationToken.None);

        Assert.Equal("LabelExact", resolved.DescribeMatch());
        Assert.False(resolved.IsLoose, "the page names the field exactly; only the timing was unlucky");
    }

    /// <summary>
    /// A page whose element arrives between the exact sweep and the loose one.
    /// </summary>
    private sealed class ArrivesDuringTheLooseSweep(FakeElement arriving, params FakeElement[] present) : IUiElementQuery
    {
        private readonly FakePage before = new FakePage(present);
        private readonly FakePage after = new FakePage([.. present, arriving]);
        private bool looseAsked;

        public Task<int> CountAsync(UiQuerySpec spec, CancellationToken cancellationToken)
        {
            this.looseAsked |= !spec.Exact;
            return (this.looseAsked ? this.after : this.before).CountAsync(spec, cancellationToken);
        }

        public Task<IReadOnlyList<UiCandidate>> DescribeAsync(UiQuerySpec spec, int max, CancellationToken cancellationToken)
            => this.after.DescribeAsync(spec, max, cancellationToken);

        public Task<IReadOnlyList<string>> AvailableNamesAsync(UiQuerySpec spec, int max, CancellationToken cancellationToken)
            => this.after.AvailableNamesAsync(spec, max, cancellationToken);
    }

    [Fact]
    public async Task AnElementThatArrivesDuringTheLooseSweep_IsFoundExactly_NotRefusedAsAmbiguous()
    {
        // Measured on the sample app: the exact channels ran before the product link rendered, the loose one after -
        // and found the link beside a blurb that mentions it, so a correct test failed as ambiguous.
        ArrivesDuringTheLooseSweep page = new ArrivesDuringTheLooseSweep(
            new FakeElement("a", Role: "link", Name: "Anvil", Text: "Anvil"),
            new FakeElement("span", Text: "Holds an anvil, barely."));

        UiResolvedTarget resolved = await TargetResolver.ResolveAsync(
            page,
            "Anvil",
            UiSmartContext.Text,
            UiResolutionOptions.Default,
            "shop",
            "http://localhost/app/products",
            CancellationToken.None);

        Assert.False(resolved.IsLoose, "the page names the link exactly; only the timing was unlucky");
    }

    [Fact]
    public async Task AGenuineAmbiguity_IsStillRefused()
    {
        // The second look must not paper over a page that really does offer two loose answers and no exact one.
        FakePage page = new FakePage(
            new FakeElement("span", Text: "Holds an anvil, barely."),
            new FakeElement("span", Text: "Anvils, all sizes."));

        await Assert.ThrowsAsync<UiAmbiguousTargetException>(() => TargetResolver.ResolveAsync(
            page,
            "Anvil",
            UiSmartContext.Text,
            UiResolutionOptions.Default,
            "shop",
            "http://localhost/app/products",
            CancellationToken.None));
    }

    [Fact]
    public async Task AGenuinelyLooseMatchIsStillReportedAsLoose()
    {
        // The confirmation must not turn every fuzzy match into an exact one: this page really does call
        // the button something else.
        FakePage page = new FakePage(new FakeElement("button", Role: "button", Name: "Save changes"));

        UiResolvedTarget resolved = await TargetResolver.ResolveAsync(
            page,
            "Save",
            UiSmartContext.Clickable,
            UiResolutionOptions.Default,
            "shop",
            "http://localhost/settings",
            CancellationToken.None);

        Assert.Equal("RoleLoose", resolved.DescribeMatch());
        Assert.True(resolved.IsLoose);
    }

    [Fact]
    public async Task MatchingAWeakerChannelExactlyIsNotAGuess()
    {
        // A field whose only name is its placeholder is named by its placeholder. The test said the right
        // thing and the page agreed, so an audit has nothing to report - even though the channel that
        // matched is not the strongest one.
        FakePage page = new FakePage(new FakeElement("input", Placeholder: "Street"));

        UiResolvedTarget resolved = await TargetResolver.ResolveAsync(
            page,
            "Street",
            UiSmartContext.Fillable,
            UiResolutionOptions.Default,
            "shop",
            "http://localhost/checkout",
            CancellationToken.None);

        Assert.Equal("PlaceholderExact", resolved.DescribeMatch());
        Assert.True(resolved.Rank > 0, "the placeholder is a weaker channel, and the rank should say so");
        Assert.False(resolved.IsLoose, "but nothing was guessed, so the audit stays quiet");
    }
}
