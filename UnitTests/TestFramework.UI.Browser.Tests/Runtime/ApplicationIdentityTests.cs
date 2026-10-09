using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Core.Steps;
using TestFramework.UI.Browser.Configuration;
using TestFramework.UI.Browser.Extensions;
using TestFramework.UI.Browser.Runtime;
using Xunit;

namespace TestFramework.UI.Browser.Tests.Runtime;

/// <summary>
/// Two applications whose names differ only in case are two applications, on every road.
/// </summary>
/// <remarks>
/// Resource names are case-sensitive across the family, and the configuration road already treated 'shop' and 'Shop'
/// as two resources - while the run's session table did not, so steps on 'Shop' were handed 'shop''s browser
/// context and page.
/// </remarks>
public sealed class ApplicationIdentityTests
{
    [Fact]
    public async Task ApplicationsThatDifferOnlyInCase_GetABrowserSessionEach()
    {
        UiRunState state = UiRunState.For(RunContext.Detached().Variables);
        int created = 0;

        Task<UiSession> Create()
        {
            Interlocked.Increment(ref created);
            return Task.FromResult((UiSession)RuntimeHelpers.GetUninitializedObject(typeof(UiSession)));
        }

        UiSession lower = await state.SessionAsync("shop", Create, CancellationToken.None);
        UiSession upper = await state.SessionAsync("Shop", Create, CancellationToken.None);

        Assert.Equal(2, created);
        Assert.NotSame(lower, upper);
    }

    [Fact]
    public void DeclaringApplicationsThatDifferOnlyInCase_IsNotADuplicate()
    {
        UiBrowserApplications applications = new();

        applications.Add("shop", new WebAppConfig { Browser = "chromium", BaseUrl = "http://shop/" });
        applications.Add("Shop", new WebAppConfig { Browser = "chromium", BaseUrl = "http://shop-admin/" });
    }
}
