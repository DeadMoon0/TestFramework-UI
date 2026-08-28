using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;
using TestFramework.UI.Browser.Configuration;

namespace TestFramework.UI.Browser.Runtime;

/// <summary>
/// One application's live browser context and page for the duration of a run.
/// </summary>
/// <remarks>
/// <para>
/// The session outlives the step that created it, which is what makes a timeline read like a person
/// using the application: one step presses a button, the next expects what it produced, and the page in
/// between is still where the first step left it.
/// </para>
/// <para>
/// It is also why every step takes the gate before touching the page. The runner is free to execute
/// independent steps at the same time, and a page driven from two of them at once is not a race the
/// framework may leave to chance.
/// </para>
/// </remarks>
internal sealed class UiSession : IAsyncDisposable
{
    private readonly List<string> consoleErrors = new List<string>();
    private readonly object consoleSync = new object();

    private UiSession(string app, WebAppConfig config, IBrowserContext context, IPage page)
    {
        this.App = app;
        this.Config = config;
        this.Context = context;
        this.Page = page;
    }

    /// <summary>The application this session belongs to.</summary>
    public string App { get; }

    /// <summary>The configuration this session was created from.</summary>
    public WebAppConfig Config { get; }

    /// <summary>The isolated context - its own cookies and storage - this run owns.</summary>
    public IBrowserContext Context { get; }

    /// <summary>The page every step in this session drives.</summary>
    public IPage Page { get; }

    /// <summary>Serialises access, because the runner may reach two steps at the same moment.</summary>
    public SemaphoreSlim Gate { get; } = new SemaphoreSlim(1, 1);

    /// <summary>
    /// Creates the session, listening for the page's own complaints from the very first navigation.
    /// </summary>
    /// <param name="app">The application identifier.</param>
    /// <param name="config">The configuration.</param>
    /// <param name="context">The browser context.</param>
    /// <returns>The session.</returns>
    public static async Task<UiSession> CreateAsync(string app, WebAppConfig config, IBrowserContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        IPage page = await context.NewPageAsync().ConfigureAwait(false);
        UiSession session = new UiSession(app, config, context, page);

        // Subscribed before anything navigates: an application that fails while loading is exactly the
        // case that would otherwise be reported as "the element was never found".
        page.Console += (_, message) =>
        {
            if (string.Equals(message.Type, "error", StringComparison.Ordinal))
            {
                session.Record($"console: {message.Text}");
            }
        };

        page.PageError += (_, error) => session.Record($"page error: {error}");

        page.RequestFailed += (_, request) => session.Record($"request failed: {request.Method} {request.Url}");

        return session;
    }

    /// <summary>
    /// Takes everything the page complained about since the last time this was called.
    /// </summary>
    /// <remarks>
    /// Draining rather than reading, so each recorded action carries the errors that happened during it
    /// and a reader can see which interaction broke the application.
    /// </remarks>
    /// <returns>The messages, oldest first.</returns>
    public IReadOnlyList<string> DrainConsoleErrors()
    {
        lock (this.consoleSync)
        {
            if (this.consoleErrors.Count == 0)
            {
                return Array.Empty<string>();
            }

            List<string> drained = new List<string>(this.consoleErrors);
            this.consoleErrors.Clear();

            return drained;
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        // The context first, the gate last: a straggling waiter that acquires the gate mid-teardown
        // meets a closed page - an honest Playwright error naming the page - where a disposed gate
        // threw ObjectDisposedException from inside the framework instead.
        await this.Context.CloseAsync().ConfigureAwait(false);

        this.Gate.Dispose();
    }

    private void Record(string message)
    {
        lock (this.consoleSync)
        {
            // Bounded: a page in a redirect loop could otherwise fill memory with the same complaint.
            if (this.consoleErrors.Count < 50)
            {
                this.consoleErrors.Add(message);
            }
        }
    }
}
