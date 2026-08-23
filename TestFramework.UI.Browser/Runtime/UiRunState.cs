using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Core.Variables;

namespace TestFramework.UI.Browser.Runtime;

/// <summary>
/// Everything one run keeps while its browser sessions are open.
/// </summary>
/// <remarks>
/// <para>
/// A step sees the run only through the arguments of its Execute method, and the variable store is the
/// one object among them that is the same instance for every step of the same run and already exists
/// while the plan is being built. So it is the run's identity here, and the table holding this state is
/// keyed on it - weakly, so a finished run's sessions become collectable without anything having to
/// remember to clean the table up.
/// </para>
/// </remarks>
internal sealed class UiRunState
{
    private static readonly ConditionalWeakTable<VariableStore, UiRunState> States = new ConditionalWeakTable<VariableStore, UiRunState>();

    private readonly Dictionary<string, UiSession> sessions = new Dictionary<string, UiSession>(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim sessionGate = new SemaphoreSlim(1, 1);
    private readonly Lazy<string> runDirectory;
    private int cleanupClaimed;
    private int screenshotCounter;

    private UiRunState()
    {
        // Created on first write, not here: a run whose steps never took a screenshot should leave no
        // empty folder behind in a build's artifacts.
        this.runDirectory = new Lazy<string>(() => UiRunPaths.RunDirectory(
            DateTimeOffset.UtcNow,
            Guid.NewGuid().ToString("N")[..8]));
    }

    /// <summary>The folder this run writes screenshots and failure bundles into.</summary>
    public string RunDirectory => this.runDirectory.Value;

    /// <summary>
    /// The state of one run, created on first use.
    /// </summary>
    /// <param name="variableStore">The run's variable store, standing in for the run itself.</param>
    /// <returns>The run's state.</returns>
    public static UiRunState For(VariableStore variableStore)
    {
        ArgumentNullException.ThrowIfNull(variableStore);

        return States.GetValue(variableStore, static _ => new UiRunState());
    }

    /// <summary>
    /// Claims the right to create this run's single cleanup step.
    /// </summary>
    /// <remarks>
    /// Every browser step offers a cleanup step while the plan is built, but only one is needed: it
    /// closes every session the run opened. The first to ask gets it, the rest return nothing.
    /// </remarks>
    /// <returns>True for the first caller of a run, false for every later one.</returns>
    public bool TryClaimCleanup() => Interlocked.Exchange(ref this.cleanupClaimed, 1) == 0;

    /// <summary>
    /// The next file name for a screenshot, numbered so a folder reads in the order things happened.
    /// </summary>
    /// <param name="name">What the screenshot is of.</param>
    /// <returns>The file name.</returns>
    public string NextScreenshotFileName(string name)
    {
        int number = Interlocked.Increment(ref this.screenshotCounter);

        return string.Format(
            CultureInfo.InvariantCulture,
            "{0:D2}-{1}.png",
            number,
            UiRunPaths.SafeName(name, 40));
    }

    /// <summary>
    /// The session for an application, creating it on first use.
    /// </summary>
    /// <param name="app">The application identifier.</param>
    /// <param name="factory">Creates the session when this run does not have one yet.</param>
    /// <param name="cancellationToken">Cancels the creation.</param>
    /// <returns>The session.</returns>
    public async Task<UiSession> SessionAsync(
        string app,
        Func<Task<UiSession>> factory,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(factory);

        if (this.sessions.TryGetValue(app, out UiSession? existing))
        {
            return existing;
        }

        await this.sessionGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (this.sessions.TryGetValue(app, out existing))
            {
                return existing;
            }

            UiSession session = await factory().ConfigureAwait(false);
            this.sessions[app] = session;

            return session;
        }
        finally
        {
            this.sessionGate.Release();
        }
    }

    /// <summary>
    /// The sessions this run has open.
    /// </summary>
    /// <returns>The sessions.</returns>
    public IReadOnlyList<UiSession> OpenSessions()
    {
        lock (this.sessions)
        {
            return new List<UiSession>(this.sessions.Values);
        }
    }

    /// <summary>
    /// Forgets every session, after they have been closed.
    /// </summary>
    public void Clear()
    {
        lock (this.sessions)
        {
            this.sessions.Clear();
        }
    }
}
