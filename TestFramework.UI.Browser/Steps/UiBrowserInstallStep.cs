using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Core.Exceptions;
using TestFramework.Core.Steps;
using TestFramework.Core.Steps.Options;
using TestFramework.UI.Browser.Runtime;

namespace TestFramework.UI.Browser.Steps;

/// <summary>
/// Installs the browser builds the run's applications asked for with <c>Install = true</c>, before the first
/// browser step needs them.
/// </summary>
/// <remarks>
/// <para>
/// A step of its own, in the stage before the main one, so the download is something the run did and says it
/// did - which build, how long - rather than a first browser step that took a minute for reasons its own
/// log does not give. When the plan noted nothing to install, it does nothing and says nothing.
/// </para>
/// <para>
/// Playwright's installer is what runs, with the version of Playwright this package was built against, so the
/// build it puts down is the build a launch looks for. It takes its own lock on the browser cache, which is what
/// keeps two test processes of one suite - one per target framework - from downloading into the same folder at
/// once; within this process, installs are taken one at a time.
/// </para>
/// </remarks>
internal sealed class UiBrowserInstallStep : Step<EmptyStepResultContext>
{
    private static readonly SemaphoreSlim OneInstallAtATime = new(1, 1);

    /// <inheritdoc />
    public override string Name => "UI Browser Install";

    /// <inheritdoc />
    public override string Description => "Installs the browser builds the run's applications asked for, when this machine lacks them";

    /// <inheritdoc />
    public override bool DoesReturn => false;

    /// <inheritdoc />
    public override Step<EmptyStepResultContext> Clone() => new UiBrowserInstallStep().WithClonedOptions(this);

    /// <inheritdoc />
    public override StepInstance<Step<EmptyStepResultContext>, EmptyStepResultContext> GetInstance()
        => new StepInstance<Step<EmptyStepResultContext>, EmptyStepResultContext>(this);

    /// <inheritdoc />
    public override void DeclareIO(StepIOContract contract)
    {
    }

    /// <inheritdoc />
    public override async Task<EmptyStepResultContext?> Execute(RunContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach ((string engine, string? build) in UiRunState.For(context.Variables).PlannedInstalls.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            await InstallAsync(engine, build, context).ConfigureAwait(false);
        }

        return EmptyStepResultContext.Instance;
    }

    private static async Task InstallAsync(string engine, string? build, RunContext context)
    {
        await OneInstallAtATime.WaitAsync(context.Deadline.Token).ConfigureAwait(false);
        try
        {
            // Another run in this process, or another process, may have put it down since the plan looked.
            if (build is not null && PlaywrightBuilds.IsInstalled(build) == true)
            {
                context.Logger.LogInformation("The browser build '{0}' is already installed.", build);
                return;
            }

            context.Logger.LogInformation("Installing {0}, because an application of this run asked for it with Install = true and this machine does not have it.", build ?? engine);
            Stopwatch elapsed = Stopwatch.StartNew();

            // The installer cannot be stopped half-way; the step stops waiting for it when its deadline does.
            int exitCode = await Task.Run(() => Microsoft.Playwright.Program.Main(["install", engine]))
                .WaitAsync(context.Deadline.Token)
                .ConfigureAwait(false);

            if (exitCode != 0 || (build is not null && PlaywrightBuilds.IsInstalled(build) == false))
            {
                throw new FrameworkConfigurationException(
                    $"Installing {engine} for this run failed (Playwright's installer exited with {exitCode}). Its own output, above in the test output, says why.")
                {
                    RecoverySteps =
                    [
                        "A machine behind a proxy, or without internet access, cannot download it: install the browser in the pipeline before the tests run, or set \"Channel\": \"msedge\" or \"chrome\" to use one that is installed.",
                        $"From a terminal, in the test project's output folder: pwsh bin/Debug/net8.0/playwright.ps1 install --with-deps {engine}",
                    ],
                };
            }

            context.Logger.LogInformation("Installed {0} in {1:0} s.", build ?? engine, elapsed.Elapsed.TotalSeconds);
        }
        finally
        {
            OneInstallAtATime.Release();
        }
    }
}
