namespace TestFramework.UI.Browser.Resolution;

/// <summary>
/// What a plain string means, decided by the verb that received it.
/// </summary>
/// <remarks>
/// This is why <c>Click("Save")</c> needs no further ceremony: the verb already says the element must
/// be something a person can press, so the search looks at buttons and links rather than at every
/// piece of text on the page.
/// </remarks>
internal enum UiSmartContext
{
    /// <summary>Something a person can press: a button, then a link.</summary>
    Clickable = 0,

    /// <summary>Something a person can type into or choose from.</summary>
    Fillable,

    /// <summary>Something a person can tick.</summary>
    Checkable,

    /// <summary>Anything carrying text, for an expectation.</summary>
    Text,

    /// <summary>A region, group or form, for scoping a search.</summary>
    Section,
}
