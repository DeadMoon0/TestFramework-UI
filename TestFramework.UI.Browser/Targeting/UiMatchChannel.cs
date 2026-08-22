namespace TestFramework.UI.Browser.Targeting;

/// <summary>
/// One way of looking for an element on a page.
/// </summary>
/// <remarks>
/// The order these are tried in is what makes a test resilient, and it is not arbitrary: it follows
/// how a person perceives a page. What something *is* and what it is *called* (role and accessible
/// name) comes first, then what it is *labelled*, then what it *hints*, then the identifier a
/// developer attached for testing, and only then its raw text. A CSS selector is not on this ladder
/// at all - it is what a test asks for explicitly when nothing user-facing describes the element.
/// </remarks>
public enum UiMatchChannel
{
    /// <summary>ARIA role plus accessible name - what assistive technology would announce.</summary>
    Role = 0,

    /// <summary>The label of a form control.</summary>
    Label,

    /// <summary>The placeholder of a form control. A hint, not a label, so it ranks below one.</summary>
    Placeholder,

    /// <summary>The configured test id attribute, usually <c>data-testid</c>.</summary>
    TestId,

    /// <summary>Visible text content.</summary>
    Text,

    /// <summary>A CSS selector supplied by the test.</summary>
    Css,

    /// <summary>The <c>id</c> attribute.</summary>
    ElementId,
}
