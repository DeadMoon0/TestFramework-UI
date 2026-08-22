namespace TestFramework.UI.Browser.Targeting;

/// <summary>
/// How a test named the element it wants.
/// </summary>
public enum UiTargetKind
{
    /// <summary>
    /// A plain string, whose meaning comes from the verb using it: what is clickable for
    /// <c>Click</c>, what is fillable for <c>Fill</c>, what is readable for <c>Expect</c>. The most
    /// common case, and the reason a test survives a control being restyled or moved.
    /// </summary>
    Smart = 0,

    /// <summary>A button, identified by its accessible name.</summary>
    Button,

    /// <summary>A link, identified by its accessible name.</summary>
    Link,

    /// <summary>An input, textarea or select, identified by its label or placeholder.</summary>
    Field,

    /// <summary>A checkbox, identified by its label.</summary>
    Checkbox,

    /// <summary>A radio button, identified by its label.</summary>
    Radio,

    /// <summary>Any element carrying the given visible text.</summary>
    Text,

    /// <summary>An explicit ARIA role, optionally narrowed by accessible name.</summary>
    Role,

    /// <summary>A region, group or form, identified by its accessible name. Used to scope a search.</summary>
    Section,

    /// <summary>An element carrying the configured test id attribute.</summary>
    TestId,

    /// <summary>An element matched by CSS selector. The escape hatch for what nothing else describes.</summary>
    Css,

    /// <summary>An element matched by its <c>id</c> attribute.</summary>
    ElementId,
}
