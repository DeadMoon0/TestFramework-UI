using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Core.Variables;

namespace TestFramework.UI.Browser.Runtime;

/// <summary>
/// Everything one run keeps while its browser sessions are open.
/// </summary>
/// <remarks>
/// <para>
/// A live browser is not something a variable can hold - it is not data, it has to be closed, and the
/// point of it is that the page one step leaves behind is the page the next step finds. The engine has a
/// place for exactly that, one slot per type per run, and this is what goes in it.
/// </para>
/// <para>
/// It used to be a <c>ConditionalWeakTable</c> keyed on the run's variable store, on the reasoning that
/// the store is the one object every step of a run shares. That was true when it was written and is not
/// any more: a step is handed a per-attempt view of the store, so keyed on what a step receives, a retry
/// would have opened a second browser and the cleanup step would have found nothing to close. Which run
/// this is has to be something the engine says, not something this package infers from what it happens to
/// be holding.
/// </para>
/// </remarks>
internal sealed class UiRunState
{
    // Concurrent, because the dictionary used to be touched under two different primitives - the
    // gate on the create path, lock(sessions) on the read paths - which exclude nothing from each
    // other: the failure observer enumerating open sessions while a parallel step's create wrote was
    // a concurrent enumeration/mutation. One dictionary, one rule: its own thread safety covers every
    // touch, and the gate keeps doing the only job it ever had - one browser launch per application.
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, UiSession> sessions = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim sessionGate = new SemaphoreSlim(1, 1);
    private int cleanupClaimed;

    /// <summary>
    /// The state of one run, created on first use.
    /// </summary>
    /// <remarks>
    /// Takes the store rather than the run's state directly, because the two callers hold different
    /// things: a step has a context, and the plan-time hook that offers a cleanup step has only the store.
    /// Both reach the same slot.
    /// </remarks>
    /// <param name="variableStore">The run's variables, or a step's view of them.</param>
    /// <returns>The run's state.</returns>
    public static UiRunState For(VariableStore variableStore)
    {
        ArgumentNullException.ThrowIfNull(variableStore);

        return variableStore.RunState.GetOrAdd(static () => new UiRunState());
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
        => new List<UiSession>(this.sessions.Values);

    /// <summary>
    /// Forgets every session, after they have been closed.
    /// </summary>
    public void Clear()
        => this.sessions.Clear();
}
