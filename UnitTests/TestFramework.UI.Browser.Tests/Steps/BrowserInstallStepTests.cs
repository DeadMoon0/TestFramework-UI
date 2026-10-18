using System.Threading.Tasks;
using TestFramework.Core.Steps;
using TestFramework.UI.Browser.Steps;

namespace TestFramework.UI.Browser.Tests.Steps;

/// <summary>
/// A run gets one browser-install step, before its first browser step, and it installs only what the plan noted.
/// </summary>
public class BrowserInstallStepTests
{
    [Fact]
    public void ARunIsOfferedOneInstallStep_HoweverManyBrowserStepsItHas()
    {
        RunContext run = RunContext.Detached();

        StepGeneric? first = BrowserExt.Session("shop").CreatePreStep(run.Variables);
        StepGeneric? second = BrowserExt.Session("admin").CreatePreStep(run.Variables);

        Assert.IsType<UiBrowserInstallStep>(first);
        Assert.Null(second);
    }

    [Fact]
    public async Task WithNothingNoted_TheStepDownloadsNothing()
    {
        // A machine with every browser it needs, or an application that did not ask: nothing to install, nothing said.
        RunContext run = RunContext.Detached();

        await new UiBrowserInstallStep().Execute(run);
    }
}
