using TestFramework.UI.Browser.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TestFramework.Core.Variables;
using TestFramework.UI.Structure;

namespace TestFramework.UI.Browser.Scripting;

/// <summary>
/// Writes a script's result into a timeline variable, in the type the test asked for.
/// </summary>
/// <remarks>
/// One implementation per requested type, built where the verb still knows <c>T</c> - so the write is an
/// ordinary typed <c>SetVariable</c> and no reflection happens at run time.
/// </remarks>
internal interface IUiScriptResultBinder
{
    /// <summary>The type the variable is declared as.</summary>
    Type ResultType { get; }

    /// <summary>
    /// Deserializes the result and writes the variable.
    /// </summary>
    /// <param name="variableStore">The run's variables.</param>
    /// <param name="variable">The variable to write.</param>
    /// <param name="result">What the script returned.</param>
    /// <param name="scriptName">The script's name, for the failure when the result does not fit.</param>
    void Bind(VariableStore variableStore, string variable, JToken? result, string scriptName);
}

/// <summary>
/// The binder for one result type.
/// </summary>
/// <typeparam name="T">The type the test asked for.</typeparam>
internal sealed class UiScriptResultBinder<T> : IUiScriptResultBinder
{
    /// <inheritdoc />
    public Type ResultType => typeof(T);

    /// <inheritdoc />
    /// <remarks>
    /// Property names match case-insensitively, which is the serialiser's own fallback rather than a switch
    /// set here: the page's objects are camelCased and the test's records are PascalCased, and that
    /// difference carries no information.
    /// </remarks>
    public void Bind(VariableStore variableStore, string variable, JToken? result, string scriptName)
    {
        ArgumentNullException.ThrowIfNull(variableStore);

        if (result is null or { Type: JTokenType.Null or JTokenType.Undefined })
        {
            throw new InvalidOperationException(
                $"The script '{scriptName}' returned nothing, and the variable '{variable}' needs a " +
                $"{typeof(T).Name}. A function body without a return returns undefined - " +
                "'() => { window.x = 1; }' evaluates to nothing, '() => window.x = 1' to the value.");
        }

        T value;

        try
        {
            value = result.ToObject<T>()
                ?? throw new JsonSerializationException("The result deserialized to null.");
        }

        // Broader than the serialiser's own exception on purpose. A page can return any shape, and a
        // conversion that cannot be made surfaces as whatever the underlying converter threw - asking for
        // an int and getting the text "not a number" raises a FormatException, not a serialisation error.
        // Every one of these means the same thing to a reader, so every one gets the same answer: here is
        // what came back, and here is the type the variable was declared as.
        catch (Exception exception) when (exception is JsonException or FormatException or InvalidCastException or OverflowException or ArgumentException)
        {
            throw new InvalidOperationException(
                $"The script '{scriptName}' returned {UiText.Truncate(PageJson.Describe(result), 120)}, " +
                $"which does not read as the {typeof(T).Name} the variable '{variable}' was declared as.",
                exception);
        }

        variableStore.SetVariable(variable, value);
    }
}

/// <summary>
/// Runs a test's JavaScript against the page or one element of it.
/// </summary>
internal static class UiScriptRunner
{
    /// <summary>
    /// Runs a script and returns what it evaluated to.
    /// </summary>
    /// <param name="script">The script.</param>
    /// <param name="page">The page.</param>
    /// <param name="element">The element to run it on, or null to run it on the page.</param>
    /// <param name="variableStore">The run's variables, for the script's arguments.</param>
    /// <param name="budget">How long a browser call may take before the step needs the time back.</param>
    /// <param name="cancellationToken">Cancels the run.</param>
    /// <returns>The result, as the page serialized it.</returns>
    /// <exception cref="InvalidOperationException">The page threw, or the result could not leave it.</exception>
    public static async Task<JToken?> RunAsync(
        JsScript script,
        IPage page,
        ILocator? element,
        VariableStore variableStore,
        ProbeBudget budget,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(script);
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(variableStore);

        cancellationToken.ThrowIfCancellationRequested();

        // Arguments resolve here, at the step's place in the timeline, like every other verb's values.
        Dictionary<string, string?>? arguments = script.Arguments.Count == 0
            ? null
            : script.Arguments.ToDictionary(
                static argument => argument.Name,
                argument => (string?)argument.Value.GetValue(variableStore),
                StringComparer.Ordinal);

        try
        {
            return element is null
                ? await PageJson.EvaluateAsync(page, script.Source, arguments).ConfigureAwait(false)
                : await PageJson.EvaluateAsync(element, script.Source, arguments, budget.Milliseconds).ConfigureAwait(false);
        }
        catch (PlaywrightException exception)
        {
            // The page's own error text is the diagnosis; the wrapper adds what the page cannot know -
            // which script this was, and the one constraint scripts most often trip over.
            throw new InvalidOperationException(
                $"The script '{script.Name}' failed in the page: {exception.Message.Split('\n')[0].Trim()} " +
                "(A script's result must be plain data - strings, numbers, booleans, arrays, objects. " +
                "A DOM node cannot leave the page; return what you need of it, such as el.textContent.)",
                exception);
        }
    }
}
