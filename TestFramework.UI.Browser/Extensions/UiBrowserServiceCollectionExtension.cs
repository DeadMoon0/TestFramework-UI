using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TestFramework.Core.Steps;
using TestFramework.UI.Browser.Configuration;
using TestFramework.UI.Browser.Exceptions;
using TestFramework.UI.Browser.Runtime;

namespace TestFramework.UI.Browser.Extensions;

/// <summary>
/// The applications a run drives, declared in code rather than read from configuration.
/// </summary>
/// <remarks>
/// Handed to <see cref="UiBrowserServiceCollectionExtension.AddUiBrowser"/>. It collects declarations and
/// holds no services of its own, which is the point: the store it becomes is this package's business, and a
/// caller never has to name - or be able to name - the type that holds it.
/// </remarks>
public sealed class UiBrowserApplications
{
    private readonly List<KeyValuePair<string, WebAppConfig>> declared = [];

    internal UiBrowserApplications()
    {
    }

    internal IReadOnlyList<KeyValuePair<string, WebAppConfig>> Declared => this.declared;

    /// <summary>
    /// Declares one application.
    /// </summary>
    /// <param name="identifier">How steps name it, for example <c>shop</c>.</param>
    /// <param name="config">Its address, browser, viewport and timeouts.</param>
    /// <returns>This collection, for chaining.</returns>
    /// <exception cref="UiConfigurationException">The same identifier was declared twice.</exception>
    public UiBrowserApplications Add(string identifier, WebAppConfig config)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);
        ArgumentNullException.ThrowIfNull(config);

        // Refused rather than overwritten, and refused here rather than at the first step that reads it:
        // two declarations of one name is a mistake in the fixture, and the second silently winning is how
        // a suite ends up driving an application nobody meant to configure.
        if (this.declared.Any(entry => string.Equals(entry.Key, identifier, StringComparison.OrdinalIgnoreCase)))
        {
            throw UiConfigurationException.DuplicateApplication(identifier);
        }

        this.declared.Add(new KeyValuePair<string, WebAppConfig>(identifier, config));

        return this;
    }
}

/// <summary>
/// Turns this package on, for a run that assembles its own services.
/// </summary>
/// <remarks>
/// <para>
/// There is one way in, and that is deliberate. The engine finds a package's run-wide pieces by resolving
/// them from the run's services, so a piece nobody registered is a piece that never runs - and for the
/// failure observer the only symptom of that is an empty evidence folder after a failure, which nobody
/// notices until they need it. Registering the applications and registering what drives them therefore has
/// to be one act rather than two, and the type holding the applications is internal so that it cannot be
/// the first without the second.
/// </para>
/// <para>
/// What it does <em>not</em> do is nail itself shut, and the two halves are treated differently on purpose.
/// The pieces this package needs are registered idempotently and never exclusively: an observer of your own
/// joins this one rather than replacing it, and the seams designed to be swapped - where a browser comes
/// from, where an address comes from - are resolved with a fallback, so registering your own still wins.
/// Declaring the applications is the opposite: doing it twice is refused by name, because a container hands
/// out the last registration and the earlier set of applications would vanish silently.
/// </para>
/// </remarks>
public static class UiBrowserServiceCollectionExtension
{
    /// <summary>
    /// Registers the applications a run drives, and everything this package needs to drive them.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="declare">Declares the applications.</param>
    /// <returns>The service collection, for chaining.</returns>
    /// <exception cref="UiConfigurationException">
    /// Applications have already been registered on this collection - by an earlier call, or by
    /// <c>LoadUIConfig()</c> reading the <c>Ui</c> configuration section.
    /// </exception>
    public static IServiceCollection AddUiBrowser(this IServiceCollection services, Action<UiBrowserApplications> declare)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(declare);

        UiBrowserApplications applications = new UiBrowserApplications();
        declare(applications);

        AddApplications(services, applications.Declared);

        return services;
    }

    /// <summary>
    /// Registers the applications and the run-wide services together.
    /// </summary>
    /// <remarks>
    /// The one place either happens, so the configuration road and the in-code road cannot register
    /// different sets. Internal because the store is: a caller reaches this through the overload above or
    /// through <c>LoadUIConfig()</c>.
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="applications">The declared applications.</param>
    internal static void AddApplications(IServiceCollection services, IEnumerable<KeyValuePair<string, WebAppConfig>> applications)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(applications);

        // Refused rather than layered. Two stores means the container hands out whichever was registered
        // last, so the other set of applications disappears without a word - and a test then fails saying
        // an application it can see in its own fixture is not configured.
        if (services.Any(static descriptor => descriptor.ServiceType == typeof(UiConfigStore)))
        {
            throw UiConfigurationException.ApplicationsAlreadyRegistered();
        }

        services.AddSingleton(new UiConfigStore(applications));

        AddRunWideServices(services);
    }

    /// <summary>
    /// The pieces the engine drives, which exist once per run and are not the caller's to build.
    /// </summary>
    /// <remarks>
    /// <c>TryAddEnumerable</c> rather than <c>Add</c>: it is keyed on the implementation type, so calling
    /// this twice registers one observer instead of two - and two would mean two evidence folders per
    /// failure and, with the browser held open, two holds to release. A caller's own observer is a
    /// different type and still lands beside this one, because watching a run is not exclusive.
    /// </remarks>
    /// <param name="services">The service collection.</param>
    private static void AddRunWideServices(IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IStepObserver, UiFailureObserver>());
    }
}
