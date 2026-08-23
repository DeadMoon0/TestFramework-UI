using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.UI.Browser.Exceptions;
using TestFramework.UI.Browser.Targeting;

namespace TestFramework.UI.Browser.Resolution;

/// <summary>
/// Finds the element a target names, and decides what to do when the page is unhelpful.
/// </summary>
/// <remarks>
/// <para>
/// Matching runs in two sweeps: every channel exact, then every channel loose. That ordering is the
/// design's centre of gravity. Trying each channel exact-then-loose in turn would let a loose match on
/// a strong channel beat an exact match on a weaker one - a page with a "Save" label and a "Save
/// changes" button would resolve <c>Fill("Save")</c> to the button. Sweeping exact across all channels
/// first means certainty always wins over a guess, and the guess is still there when certainty finds
/// nothing.
/// </para>
/// <para>
/// The resolver never touches a browser. It asks how many elements a lookup finds and reasons about
/// the answer, so every ordering rule, every ambiguity decision and every failure message can be
/// tested exhaustively without one.
/// </para>
/// </remarks>
internal static class TargetResolver
{
    /// <summary>
    /// Finds the element a target names.
    /// </summary>
    /// <param name="query">The page to ask.</param>
    /// <param name="target">The element to find.</param>
    /// <param name="context">What a plain string means for the verb doing the asking.</param>
    /// <param name="options">The dials that apply to this session.</param>
    /// <param name="app">The application identifier, for failure messages.</param>
    /// <param name="url">The current address, for failure messages.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns>Which lookup found the element, which match to act on, and how good the match was.</returns>
    /// <exception cref="UiAmbiguousTargetException">Several elements answered and the test did not say
    /// which one it meant.</exception>
    /// <exception cref="UiTargetNotFoundException">Nothing answered.</exception>
    public static async Task<UiResolvedTarget> ResolveAsync(
        IUiElementQuery query,
        UiTarget target,
        UiSmartContext context,
        UiResolutionOptions options,
        string app,
        string url,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(options);

        UiQuerySpec? scopeSpec = null;
        int scopeIndex = 0;

        if (target.Scope is { } scope)
        {
            // The scope has to resolve to one element before anything can be searched inside it, and it
            // resolves by the same rules - including failing loudly when a page has two "Billing"
            // sections, which is exactly when scoping was supposed to help.
            UiResolvedTarget resolvedScope = await ResolveAsync(
                query,
                scope,
                UiSmartContext.Section,
                options,
                app,
                url,
                cancellationToken).ConfigureAwait(false);

            scopeSpec = resolvedScope.Spec;
            scopeIndex = resolvedScope.Index;
        }

        IReadOnlyList<UiChannelStep> ladder = UiChannelLadder.For(target, context);
        List<UiQuerySpec> tried = new List<UiQuerySpec>();

        UiResolvedTarget? exactMatch = await SweepAsync(query, target, ladder, scopeSpec, scopeIndex, options, app, url, exact: true, tried, cancellationToken)
            .ConfigureAwait(false);

        if (exactMatch is not null)
        {
            return exactMatch;
        }

        if (target.Exact)
        {
            // The test asked for an exact match and there is not one. Guessing here would be the opposite
            // of what it asked.
            throw await NotFoundAsync(query, target, ladder, scopeSpec, scopeIndex, options, app, url, tried, cancellationToken)
                .ConfigureAwait(false);
        }

        UiResolvedTarget? fuzzyMatch = await SweepAsync(query, target, ladder, scopeSpec, scopeIndex, options, app, url, exact: false, tried, cancellationToken)
            .ConfigureAwait(false);

        if (fuzzyMatch is null)
        {
            throw await NotFoundAsync(query, target, ladder, scopeSpec, scopeIndex, options, app, url, tried, cancellationToken)
                .ConfigureAwait(false);
        }

        // A page can finish rendering in the middle of a sweep - every channel is a separate round trip -
        // and then an exact channel that answered "nothing" a moment ago would answer "here it is" now.
        // Accepting the fuzzy match at that point would report a guess where the test was precise, and
        // would quietly downgrade the match quality an audit relies on. So a fuzzy win gets a second look
        // at the exact sweep before it stands.
        List<UiQuerySpec> secondLook = new List<UiQuerySpec>();

        UiResolvedTarget? confirmed = await SweepAsync(query, target, ladder, scopeSpec, scopeIndex, options, app, url, exact: true, secondLook, cancellationToken)
            .ConfigureAwait(false);

        return confirmed ?? fuzzyMatch;
    }

    private static async Task<UiResolvedTarget?> SweepAsync(
        IUiElementQuery query,
        UiTarget target,
        IReadOnlyList<UiChannelStep> ladder,
        UiQuerySpec? scopeSpec,
        int scopeIndex,
        UiResolutionOptions options,
        string app,
        string url,
        bool exact,
        List<UiQuerySpec> tried,
        CancellationToken cancellationToken)
    {
        // Ranks continue across the sweeps, so a fuzzy win is visibly further down the ladder than any
        // exact one.
        int rank = exact ? 0 : ladder.Count;

        foreach (UiChannelStep step in ladder)
        {
            if (!exact && UiChannelLadder.IsExactOnly(step.Channel))
            {
                // A selector or a test id has no loose form; repeating it would only inflate the rank of
                // whatever matches next.
                continue;
            }

            UiQuerySpec spec = new UiQuerySpec(
                step.Channel,
                Text: TextFor(target, step.Channel),
                Exact: exact,
                Role: step.Role,
                Css: target.Css,
                NearText: target.NearText,
                Within: scopeSpec,
                WithinIndex: scopeIndex);

            tried.Add(spec);

            int count = await query.CountAsync(spec, cancellationToken).ConfigureAwait(false);

            if (count == 0)
            {
                rank++;
                continue;
            }

            if (count == 1)
            {
                return new UiResolvedTarget(spec, 0, rank, 1, await SnippetAsync(query, spec, 0, cancellationToken).ConfigureAwait(false));
            }

            return await ResolveAmbiguityAsync(query, target, options, app, url, spec, count, rank, cancellationToken)
                .ConfigureAwait(false);
        }

        return null;
    }

    private static async Task<UiTargetNotFoundException> NotFoundAsync(
        IUiElementQuery query,
        UiTarget target,
        IReadOnlyList<UiChannelStep> ladder,
        UiQuerySpec? scopeSpec,
        int scopeIndex,
        UiResolutionOptions options,
        string app,
        string url,
        List<UiQuerySpec> tried,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<string> availableNames = await AvailableNamesAsync(
            query,
            ladder,
            scopeSpec,
            scopeIndex,
            options,
            cancellationToken).ConfigureAwait(false);

        return new UiTargetNotFoundException(app, url, target, tried, availableNames);
    }

    private static async Task<UiResolvedTarget> ResolveAmbiguityAsync(
        IUiElementQuery query,
        UiTarget target,
        UiResolutionOptions options,
        string app,
        string url,
        UiQuerySpec spec,
        int count,
        int rank,
        CancellationToken cancellationToken)
    {
        if (target.Index is { } index)
        {
            if (index >= count)
            {
                throw new UiAmbiguousTargetException(
                    app,
                    url,
                    target,
                    spec,
                    count,
                    await query.DescribeAsync(spec, options.MaxCandidatesReported, cancellationToken).ConfigureAwait(false));
            }

            return new UiResolvedTarget(
                spec,
                index,
                rank,
                count,
                await SnippetAsync(query, spec, index, cancellationToken).ConfigureAwait(false));
        }

        if (target.AllowFirstOfMany || options.AmbiguityMode == UiAmbiguityMode.FirstMatch)
        {
            return new UiResolvedTarget(
                spec,
                0,
                rank,
                count,
                await SnippetAsync(query, spec, 0, cancellationToken).ConfigureAwait(false));
        }

        throw new UiAmbiguousTargetException(
            app,
            url,
            target,
            spec,
            count,
            await query.DescribeAsync(spec, options.MaxCandidatesReported, cancellationToken).ConfigureAwait(false));
    }

    private static string? TextFor(UiTarget target, UiMatchChannel channel)
        // A selector carries its own text; every other channel matches on the name the test gave.
        => channel is UiMatchChannel.Css ? null : target.Name;

    private static async Task<string?> SnippetAsync(
        IUiElementQuery query,
        UiQuerySpec spec,
        int index,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<UiCandidate> candidates = await query
            .DescribeAsync(spec, index + 1, cancellationToken)
            .ConfigureAwait(false);

        return index < candidates.Count ? candidates[index].Snippet : null;
    }

    private static async Task<IReadOnlyList<string>> AvailableNamesAsync(
        IUiElementQuery query,
        IReadOnlyList<UiChannelStep> ladder,
        UiQuerySpec? scopeSpec,
        int scopeIndex,
        UiResolutionOptions options,
        CancellationToken cancellationToken)
    {
        List<string> names = new List<string>();

        foreach (UiChannelStep step in ladder)
        {
            if (names.Count >= options.MaxNamesSuggested)
            {
                break;
            }

            UiQuerySpec spec = new UiQuerySpec(
                step.Channel,
                Text: null,
                Exact: false,
                Role: step.Role,
                Within: scopeSpec,
                WithinIndex: scopeIndex);

            IReadOnlyList<string> channelNames = await query
                .AvailableNamesAsync(spec, options.MaxNamesSuggested - names.Count, cancellationToken)
                .ConfigureAwait(false);

            foreach (string name in channelNames)
            {
                if (!string.IsNullOrWhiteSpace(name) && !names.Contains(name, StringComparer.Ordinal))
                {
                    names.Add(name);
                }
            }
        }

        return names;
    }
}
