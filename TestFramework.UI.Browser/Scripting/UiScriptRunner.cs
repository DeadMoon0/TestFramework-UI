using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;
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
    void Bind(VariableStore variableStore, string variable, JsonElement? result, string scriptName);
}

/// <summary>
/// The binder for one result type.
/// </summary>
/// <typeparam name="T">The type the test asked for.</typeparam>
internal sealed class UiScriptResultBinder<T> : IUiScriptResultBinder
{
    /// <remarks>
    /// Case-insensitive property names, because the page's objects are camelCased and the test's records
    /// are PascalCased, and that difference carries no information.
    /// </remarks>
    private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <inheritdoc />
    public Type ResultType => typeof(T);

    /// <inheritdoc />
    public void Bind(VariableStore variableStore, string variable, JsonElement? result, string scriptName)
    {
        ArgumentNullException.ThrowIfNull(variableStore);

        if (result is null or { ValueKind: JsonValueKind.Null or JsonValueKind.Undefined })
        {
            throw new InvalidOperationException(
                $"The script '{scriptName}' returned nothing, and the variable '{variable}' needs a " +
                $"{typeof(T).Name}. A function body without a return returns undefined - " +
                "'() => { window.x = 1; }' evaluates to nothing, '() => window.x = 1' to the value.");
        }

        T value;

        try
        {
            value = result.Value.Deserialize<T>(Options)
                ?? throw new JsonException("The result deserialized to null.");
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                $"The script '{scriptName}' returned {UiText.Truncate(result.Value.GetRawText(), 120)}, " +
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
    /// <param name="cancellationToken">Cancels the run.</param>
    /// <returns>The result, as the page serialized it.</returns>
    /// <exception cref="InvalidOperationException">The page threw, or the result could not leave it.</exception>
    public static async Task<JsonElement?> RunAsync(
        JsScript script,
        IPage page,
        ILocator? element,
        VariableStore variableStore,
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
                ? await page.EvaluateAsync(script.Source, arguments).ConfigureAwait(false)
                : await element.EvaluateAsync(script.Source, arguments).ConfigureAwait(false);
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
