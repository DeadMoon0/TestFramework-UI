using System;

namespace TestFramework.UI.Session;

/// <summary>
/// The naming convention for the variable that carries an application's session picture through a run.
/// </summary>
/// <remarks>
/// The prefix keeps these out of the way of a test author's own variable names, and makes them
/// recognisable in the debugging UI's variable list as belonging to a UI session rather than to the
/// test's data.
/// </remarks>
public static class UiSessionVariable
{
    /// <summary>
    /// The prefix every session variable name starts with.
    /// </summary>
    public const string Prefix = "ui:";

    /// <summary>
    /// The variable name carrying the session picture of one application.
    /// </summary>
    /// <param name="app">The application identifier.</param>
    /// <returns>The variable name, for example <c>ui:shop</c>.</returns>
    public static string For(string app)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(app);

        return Prefix + app;
    }

    /// <summary>
    /// Determines whether a variable name is a session variable.
    /// </summary>
    /// <param name="identifier">The variable name to test.</param>
    /// <returns>True when the name follows this convention.</returns>
    public static bool IsSessionVariable(string? identifier)
        => identifier is not null && identifier.StartsWith(Prefix, StringComparison.Ordinal);
}
