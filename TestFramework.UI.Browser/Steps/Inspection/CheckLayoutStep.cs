using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;
using TestFramework.Core.Steps;
using TestFramework.UI.Browser.Exceptions;
using TestFramework.UI.Browser.Identifier;
using TestFramework.UI.Browser.Layouting;
using TestFramework.UI.Browser.Resolution;
using TestFramework.UI.Browser.Targeting;
using TestFramework.UI.Layout;

namespace TestFramework.UI.Browser.Steps.Inspection;

/// <summary>
/// Checks that the page is laid out the way a layout says.
/// </summary>
/// <remarks>
/// Retried until the layout settles, like every comparison: a page mid-render has elements at places
/// they are only passing through. Each look resolves every named element fresh - by the same rules the
/// verbs use, strictness included - measures them all in one moment, and evaluates every relation, so a
/// failure lists everything wrong at once rather than the first thing.
/// </remarks>
internal sealed class CheckLayoutStep : UiInspectionStep<UiCompareResultContext>
{
    private readonly ExpectedLayout layout;
    private readonly IReadOnlyList<UiLayoutRule> rules;

    public CheckLayoutStep(WebAppIdentifier app, ExpectedLayout layout)
        : base(app)
    {
        ArgumentNullException.ThrowIfNull(layout);

        this.layout = layout;
        this.rules = layout.Compile();
    }

    /// <inheritdoc />
    public override string Name => "UI Check Layout";

    /// <inheritdoc />
    public override string Description => $"Checks {this.layout.Describe()} on '{this.App}'";

    /// <inheritdoc />
    protected override string ActionName => "CheckLayout";

    /// <inheritdoc />
    public override Step<UiCompareResultContext> Clone()
        => new CheckLayoutStep(this.App, this.layout).WithClonedOptions(this);

    /// <inheritdoc />
    public override StepInstance<Step<UiCompareResultContext>, UiCompareResultContext> GetInstance()
        => new StepInstance<Step<UiCompareResultContext>, UiCompareResultContext>(this);

    /// <inheritdoc />
    protected override async Task<(UiCompareResultContext Result, string? Detail)> InspectAsync(
        ILocator? locator,
        CancellationToken cancellationToken)
    {
        UiInspectionLens lens = this.Lens!;
        List<string> differences = new List<string>();
        Dictionary<string, UiBox> boxes = new Dictionary<string, UiBox>(StringComparer.Ordinal);

        foreach (UiTarget target in this.DistinctTargets())
        {
            if (await this.MeasureAsync(lens, target, cancellationToken).ConfigureAwait(false) is { } box)
            {
                boxes[target.Describe()] = box;
            }
            else
            {
                // Not there, or there without a box: both read as "not yet" to the settle loop, and as a
                // plain fact once time runs out.
                differences.Add($"{target.Describe()} is not on the page, or has no size to measure");
            }
        }

        if (differences.Count == 0)
        {
            await this.EvaluateAsync(lens, boxes, differences, cancellationToken).ConfigureAwait(false);
        }

        string actual = string.Join(
            "\n",
            boxes.Select(static pair => string.Create(CultureInfo.InvariantCulture, $"  {pair.Key} at {pair.Value}")));

        UiCompareResultContext result = new UiCompareResultContext(
            this.App,
            lens.Session.Page.Url,
            "the layout",
            differences.Count == 0,
            differences,
            actual,
            SuggestedCode: null);

        return (result, differences.Count == 0 ? $"matched ({this.rules.Count} relations)" : $"{differences.Count} difference(s)");
    }

    /// <inheritdoc />
    protected override bool ShouldRetry(UiCompareResultContext result) => !result.IsMatch;

    /// <inheritdoc />
    protected override Exception? Verdict(UiCompareResultContext result, TimeSpan waited)
        => result.IsMatch
            ? null
            : new UiLayoutMismatchException(result.App, result.Url, result.Differences, result.Actual, waited);

    private IEnumerable<UiTarget> DistinctTargets()
    {
        HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (UiLayoutRule rule in this.rules)
        {
            foreach (UiTarget? target in new[] { rule.First, rule.Second })
            {
                if (target is not null && seen.Add(target.Describe()))
                {
                    yield return target;
                }
            }
        }
    }

    private async Task<UiBox?> MeasureAsync(UiInspectionLens lens, UiTarget target, CancellationToken cancellationToken)
    {
        try
        {
            UiResolvedTarget resolved = await TargetResolver.ResolveAsync(
                lens.Query,
                target,
                UiSmartContext.Text,
                lens.Options,
                this.App,
                lens.Session.Page.Url,
                cancellationToken).ConfigureAwait(false);

            // One immediate measurement, no waiting of its own - the settle loop owns the waiting, and
            // all boxes of one look belong to one moment.
            LocatorBoundingBoxResult? box = await lens.Query.Locate(resolved).BoundingBoxAsync().ConfigureAwait(false);

            return box is null ? null : new UiBox(box.X, box.Y, box.Width, box.Height);
        }
        catch (UiTargetNotFoundException)
        {
            return null;
        }
    }

    private async Task EvaluateAsync(
        UiInspectionLens lens,
        IReadOnlyDictionary<string, UiBox> boxes,
        List<string> differences,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        foreach (UiLayoutRule rule in this.rules)
        {
            switch (rule.Relation)
            {
                case UiLayoutRelation.NoHorizontalScroll:
                    double overflow = await lens.Session.Page
                        .EvaluateAsync<double>("() => Math.max(document.documentElement.scrollWidth - document.documentElement.clientWidth, 0)")
                        .ConfigureAwait(false);

                    if (overflow > UiBoxRelations.Tolerance)
                    {
                        differences.Add(string.Create(
                            CultureInfo.InvariantCulture,
                            $"the page scrolls sideways by {overflow:F0}px"));
                    }

                    break;

                case UiLayoutRelation.InViewport:
                    UiBox subject = boxes[rule.First!.Describe()];
                    UiBox viewport = await this.ViewportAsync(lens).ConfigureAwait(false);

                    if (!UiBoxRelations.IsInside(subject, viewport))
                    {
                        differences.Add(
                            $"{rule.First.Describe()} should be within the viewport {viewport}, but is at {subject}");
                    }

                    break;

                default:
                    this.EvaluatePair(rule, boxes, differences);
                    break;
            }
        }
    }

    private void EvaluatePair(UiLayoutRule rule, IReadOnlyDictionary<string, UiBox> boxes, List<string> differences)
    {
        UiBox first = boxes[rule.First!.Describe()];
        UiBox second = boxes[rule.Second!.Describe()];

        bool satisfied = rule.Relation switch
        {
            UiLayoutRelation.Above => UiBoxRelations.IsAbove(first, second),
            UiLayoutRelation.LeftOf => UiBoxRelations.IsLeftOf(first, second),
            UiLayoutRelation.Inside => UiBoxRelations.IsInside(first, second),
            UiLayoutRelation.NotOverlapping => !UiBoxRelations.Overlap(first, second),
            _ => throw new InvalidOperationException($"Unknown relation '{rule.Relation}'."),
        };

        if (!satisfied)
        {
            // Both actual boxes, so the reader sees the page's answer without opening it.
            differences.Add(
                $"expected {rule.Describe()}, but {rule.First.Describe()} is at {first} and {rule.Second.Describe()} is at {second}");
        }
    }

    private async Task<UiBox> ViewportAsync(UiInspectionLens lens)
    {
        // The configured viewport when there is one; the page's own answer otherwise - never a guess.
        if (lens.Session.Page.ViewportSize is { } size)
        {
            return new UiBox(0, 0, size.Width, size.Height);
        }

        int[] measured = await lens.Session.Page
            .EvaluateAsync<int[]>("() => [window.innerWidth, window.innerHeight]")
            .ConfigureAwait(false) ?? throw new InvalidOperationException("The page reported no viewport.");

        return new UiBox(0, 0, measured[0], measured[1]);
    }
}
