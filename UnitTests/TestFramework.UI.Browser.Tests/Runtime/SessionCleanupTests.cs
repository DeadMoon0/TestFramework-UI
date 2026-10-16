using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TestFramework.UI.Browser.Steps;

namespace TestFramework.UI.Browser.Tests.Runtime;

/// <summary>
/// Cleanup closes every session a run opened, however any one of them fails to close.
/// </summary>
public class SessionCleanupTests
{
    [Fact]
    public async Task ASessionThatThrowsOnClose_DoesNotKeepTheOthersOpen()
    {
        List<string> closed = new List<string>();
        List<string> reported = new List<string>();

        await UiSessionCleanupStep.CloseEachAsync<string>(
            new[] { "shop", "admin", "portal" },
            app => app == "admin" ? throw new InvalidOperationException("context gone") : Record(closed, app),
            (app, _) => reported.Add(app));

        Assert.Equal(new[] { "shop", "portal" }, closed);
        Assert.Equal(new[] { "admin" }, reported);
    }

    [Fact]
    public async Task ACancelledClose_StillClosesEveryOtherSession_AndThenReportsTheCancellation()
    {
        List<string> closed = new List<string>();
        List<string> reported = new List<string>();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => UiSessionCleanupStep.CloseEachAsync<string>(
            new[] { "shop", "admin", "portal" },
            app => app == "shop" ? throw new OperationCanceledException() : Record(closed, app),
            (app, _) => reported.Add(app)));

        Assert.Equal(new[] { "admin", "portal" }, closed);
        Assert.Equal(new[] { "shop" }, reported);
    }

    private static Task Record(List<string> closed, string app)
    {
        closed.Add(app);

        return Task.CompletedTask;
    }
}
