using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;
using TestFramework.UI.Browser.Reading;
using TestFramework.UI.Structure;

namespace TestFramework.UI.Browser.Steps;

/// <summary>
/// Chooses an option from whatever kind of list control a page actually has.
/// </summary>
/// <remarks>
/// <para>
/// Two contracts, both of them standards rather than libraries: a native <c>&lt;select&gt;</c>, driven
/// directly, and the ARIA combobox pattern - an element declaring <c>role="combobox"</c> or
/// <c>aria-haspopup="listbox"</c> that opens a <c>role="listbox"</c> of <c>role="option"</c>s. Angular
/// Material's select is the second kind, and so is every other component library's; nothing here knows
/// any of their names.
/// </para>
/// <para>
/// Options are matched the way targets are: by what a person sees, exactly first and loosely only when
/// nothing matches exactly, and never silently when several answer. A failure lists the options the
/// control does offer, so the fix is a copy.
/// </para>
/// </remarks>
internal static class UiChooser
{
    /// <summary>
    /// Chooses an option on a resolved list control.
    /// </summary>
    /// <param name="control">The list control.</param>
    /// <param name="page">The page it is on, for finding a combobox's popup.</param>
    /// <param name="option">The option, as a person would name it.</param>
    /// <param name="describeTarget">How the control reads in messages.</param>
    /// <param name="timeout">How long a combobox's popup is given to appear.</param>
    /// <param name="cancellationToken">Cancels the choice.</param>
    /// <returns>A trace line naming what was chosen and on which kind of control.</returns>
    public static async Task<string> ChooseAsync(
        ILocator control,
        IPage page,
        string option,
        string describeTarget,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(control);
        ArgumentNullException.ThrowIfNull(page);
        ArgumentException.ThrowIfNullOrWhiteSpace(option);

        string tag = await control.EvaluateAsync<string>("el => el.tagName.toLowerCase()").ConfigureAwait(false);

        if (tag == "select")
        {
            return await ChooseNativeAsync(control, option, describeTarget).ConfigureAwait(false);
        }

        if (await UiValueReader.IsComboboxAsync(control).ConfigureAwait(false))
        {
            return await ChooseFromComboboxAsync(control, page, option, describeTarget, timeout, cancellationToken)
                .ConfigureAwait(false);
        }

        throw new InvalidOperationException(
            $"The {describeTarget} is a <{tag}> that is neither a native <select> nor declares the combobox " +
            "contract (role=\"combobox\" or aria-haspopup=\"listbox\"), so there is no list here to choose from.");
    }

    private static async Task<string> ChooseNativeAsync(ILocator control, string option, string describeTarget)
    {
        // Labels and values both, because a test says "Express" while the markup says "express" - and
        // which of the two the author used should not decide whether the test works.
        IReadOnlyList<string[]> options = await control
            .EvaluateAsync<string[][]>("el => Array.from(el.options, o => [o.label, o.value])")
            .ConfigureAwait(false) ?? [];

        int match = Match(
            option,
            options.Select(static entry => entry[0]).ToList(),
            options.Select(static entry => entry[1]).ToList(),
            describeTarget);

        await control.SelectOptionAsync(new[] { new SelectOptionValue { Index = match } }).ConfigureAwait(false);

        return $"'{options[match][0]}' (native select)";
    }

    private static async Task<string> ChooseFromComboboxAsync(
        ILocator control,
        IPage page,
        string option,
        string describeTarget,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        await control.ClickAsync().ConfigureAwait(false);

        ILocator listbox = await FindListboxAsync(control, page, describeTarget, timeout, cancellationToken)
            .ConfigureAwait(false);

        ILocator optionElements = listbox.GetByRole(AriaRole.Option);

        // The popup is open but may still be filling; its first settled state is what a person chooses
        // from, and waiting for at least one option is how "still filling" and "genuinely empty" differ.
        await optionElements.First
            .WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = (float)timeout.TotalMilliseconds })
            .ConfigureAwait(false);

        IReadOnlyList<string> labels = (await optionElements.AllInnerTextsAsync().ConfigureAwait(false))
            .Select(static text => UiText.Normalize(text) ?? string.Empty)
            .ToList();

        int match = Match(option, labels, values: null, describeTarget);

        await optionElements.Nth(match).ClickAsync().ConfigureAwait(false);

        // The choice is not over until the popup is gone: a single-choice combobox closes on selection,
        // and while its listbox is still animating out it is still on the page - where the very next
        // lookup would find it, labelled like the control it belongs to, and rightly call that ambiguous.
        try
        {
            await listbox
                .WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Hidden, Timeout = (float)timeout.TotalMilliseconds })
                .ConfigureAwait(false);
        }
        catch (PlaywrightException)
        {
            throw new System.TimeoutException(
                $"'{labels[match]}' was chosen on the {describeTarget}, but its popup did not close within " +
                $"{timeout.TotalSeconds:F0}s. A list that stays open after a choice is outside the " +
                "single-choice contract this verb drives.");
        }

        return $"'{labels[match]}' (combobox)";
    }

    /// <summary>
    /// Finds the listbox an opened combobox is offering.
    /// </summary>
    /// <remarks>
    /// The combobox's own <c>aria-controls</c> names it when the page follows the pattern fully. When it
    /// does not, the one visible listbox on the page is an unambiguous answer too; several visible
    /// listboxes are not, and that fails rather than guessing which one the click opened.
    /// </remarks>
    private static async Task<ILocator> FindListboxAsync(
        ILocator control,
        IPage page,
        string describeTarget,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string? controls = await control.GetAttributeAsync("aria-controls").ConfigureAwait(false);

            if (controls is { Length: > 0 })
            {
                // aria-controls may name several ids; the listbox among them is the one meant here.
                foreach (string id in controls.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    ILocator candidate = page.Locator($"[id='{id}']");

                    if (await candidate.CountAsync().ConfigureAwait(false) == 1
                        && string.Equals(
                            await candidate.GetAttributeAsync("role").ConfigureAwait(false),
                            "listbox",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return candidate;
                    }
                }
            }

            ILocator visible = page.GetByRole(AriaRole.Listbox);
            int count = await visible.CountAsync().ConfigureAwait(false);

            if (count == 1)
            {
                return visible;
            }

            if (count > 1)
            {
                throw new InvalidOperationException(
                    $"The {describeTarget} opened, but the page shows {count} listboxes and its aria-controls " +
                    "does not say which one belongs to it, so choosing would be a guess.");
            }

            if (DateTimeOffset.UtcNow >= deadline)
            {
                throw new TimeoutException(
                    $"The {describeTarget} was clicked, but no listbox appeared within {timeout.TotalSeconds:F0}s. " +
                    "A combobox that opens elsewhere than a role=\"listbox\" popup is outside the contract this " +
                    "verb drives.");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(80), cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Finds the option an author meant, the way targets are matched: exact everywhere first, loose only
    /// as a fallback, ambiguity and absence both loud.
    /// </summary>
    private static int Match(string option, IReadOnlyList<string> labels, IReadOnlyList<string>? values, string describeTarget)
    {
        string wanted = UiText.Normalize(option) ?? string.Empty;

        foreach (bool exact in new[] { true, false })
        {
            List<int> matches = Enumerable.Range(0, labels.Count)
                .Where(index => Matches(labels[index], wanted, exact) || (values is not null && Matches(values[index], wanted, exact)))
                .ToList();

            if (matches.Count == 1)
            {
                return matches[0];
            }

            if (matches.Count > 1)
            {
                throw new InvalidOperationException(
                    $"{matches.Count} options of the {describeTarget} answer to '{option}': " +
                    $"{string.Join(", ", matches.Select(index => $"'{labels[index]}'"))}. Name one of them exactly.");
            }
        }

        string offered = labels.Count == 0
            ? "It offers no options at all."
            : $"It offers: {string.Join(", ", labels.Select(static label => $"'{label}'"))}.";

        throw new InvalidOperationException($"The {describeTarget} has no option '{option}'. {offered}");
    }

    private static bool Matches(string candidate, string wanted, bool exact)
    {
        string? normalized = UiText.Normalize(candidate);

        return exact
            ? string.Equals(normalized, wanted, StringComparison.Ordinal)
            : normalized?.Contains(wanted, StringComparison.OrdinalIgnoreCase) == true;
    }
}
