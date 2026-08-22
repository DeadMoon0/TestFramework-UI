namespace TestFramework.UI.Browser.Resolution;

/// <summary>
/// What to do when a name matches more than one element.
/// </summary>
public enum UiAmbiguityMode
{
    /// <summary>
    /// Fail, listing the candidates and the dial that resolves it. The default, because pressing one
    /// of three buttons called Delete is not a decision a test framework should make quietly.
    /// </summary>
    Strict = 0,

    /// <summary>
    /// Use the first match and record that a choice was made. For pages that genuinely offer
    /// equivalent controls, where naming each one precisely would only add noise. The choice still
    /// lands in the session picture, so <c>run.UiWeakestMatch(...)</c> can hold a suite to account.
    /// </summary>
    FirstMatch,
}
