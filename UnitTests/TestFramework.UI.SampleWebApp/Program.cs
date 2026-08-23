using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace TestFramework.UI.SampleWebApp;

/// <summary>
/// The application the browser suite tests against.
/// </summary>
/// <remarks>
/// <para>
/// It serves two things. The pages under <c>wwwroot</c> are hand-written fixtures, each one a single
/// resilience case - a control that was renamed, one that moved, three that are genuinely ambiguous,
/// content that arrives late. They have no build step on purpose: when a test on one of them fails, the
/// question is about the framework, never about tooling.
/// </para>
/// <para>
/// The second is the Angular application in <c>../TestFramework.UI.SampleApp</c>, served from its build
/// output when that output exists. That one is the realistic case - components, custom element names,
/// routing, a data table - and it is where structure comparison has to prove itself against markup
/// nobody hand-tuned for the test.
/// </para>
/// </remarks>
public static class Program
{
    /// <summary>The route prefix the Angular application is served under.</summary>
    public const string AngularPathBase = "/app";

    /// <summary>
    /// The configuration key naming where the Angular build output is.
    /// </summary>
    /// <remarks>
    /// Needed because a test fixture starts this application inside the test host, where the content root
    /// is the test project's own output folder and the path relative to it means nothing.
    /// </remarks>
    public const string AngularRootSetting = "AngularRoot";

    /// <summary>
    /// Starts the host.
    /// </summary>
    /// <param name="args">Command line arguments, passed to the host builder.</param>
    public static void Main(string[] args) => CreateApp(args).Run();

    /// <summary>
    /// Builds the host without starting it.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="Main"/> so a test fixture can start the very same application in its own
    /// process on a port the operating system picks. The browser suite then drives a real host over a real
    /// socket - the point being to exercise what a browser actually does, which an in-memory test host
    /// could not.
    /// </remarks>
    /// <param name="args">Command line arguments, passed to the host builder.</param>
    /// <returns>The configured application.</returns>
    public static WebApplication CreateApp(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
        WebApplication app = builder.Build();

        string? angularRoot = ResolveAngularRoot(app);

        app.UseDefaultFiles();
        app.UseStaticFiles();

        if (angularRoot is not null)
        {
            ServeAngularFiles(app, angularRoot);
        }

        // Explicit, and deliberately after every file server: the single-page fallback below matches every
        // path under the application's prefix, and once an endpoint has been selected the static file
        // middleware steps aside - which would answer every script request with the shell instead of the
        // script. Routing therefore has to come after the files, not before them.
        app.UseRouting();

        MapOrdersApi(app);
        MapAngularFallback(app, angularRoot);

        // Lets a test fixture confirm the host is up before it drives a browser at it.
        app.MapGet("/health", static () => Results.Ok(new { status = "ok" }));

        return app;
    }

    /// <summary>
    /// Where the Angular application's build output is expected.
    /// </summary>
    /// <param name="contentRoot">The host's content root.</param>
    /// <returns>The folder, whether or not it exists.</returns>
    public static string AngularOutputDirectory(string contentRoot)
        => Path.GetFullPath(Path.Combine(contentRoot, "..", "TestFramework.UI.SampleApp", "dist", "sample-app", "browser"));

    private static void MapOrdersApi(WebApplication app)
    {
        // Just enough of an API for the Angular application to have something to load, and for a
        // timeline to verify through a second door what it did through the first.
        List<SampleOrder> orders =
        [
            new SampleOrder("A-1001", "Anvil", 1, 129.00m, "Shipped"),
            new SampleOrder("A-1002", "Rope", 3, 9.00m, "Packing"),
            new SampleOrder("A-1003", "Crate", 2, 24.50m, "Packing"),
        ];

        app.MapGet("/api/orders", () => Results.Ok(orders));

        app.MapGet("/api/orders/{id}", (string id) =>
            orders.FirstOrDefault(order => string.Equals(order.Id, id, StringComparison.OrdinalIgnoreCase)) is { } found
                ? Results.Ok(found)
                : Results.NotFound());

        app.MapPost("/api/orders", (SampleOrder order) =>
        {
            orders.Add(order);

            return Results.Created($"/api/orders/{order.Id}", order);
        });
    }

    private static string? ResolveAngularRoot(WebApplication app)
    {
        string angularRoot = app.Configuration[AngularRootSetting] is { Length: > 0 } configured
            ? Path.GetFullPath(configured)
            : AngularOutputDirectory(app.Environment.ContentRootPath);

        // Optional on purpose: a machine without a Node toolchain can still run everything that does not
        // need the Angular application, and the test gate skips the rest with its reason.
        return Directory.Exists(angularRoot) ? angularRoot : null;
    }

    private static void ServeAngularFiles(WebApplication app, string angularRoot)
        => app.UseFileServer(new FileServerOptions
        {
            FileProvider = new PhysicalFileProvider(angularRoot),
            RequestPath = AngularPathBase,
            EnableDefaultFiles = true,
        });

    private static void MapAngularFallback(WebApplication app, string? angularRoot)
    {
        if (angularRoot is null)
        {
            app.MapGet(AngularPathBase, static () => Results.NotFound(
                "The Angular sample application has not been built. Run 'npm ci && npm run build' in " +
                "UnitTests/TestFramework.UI.SampleApp."));

            return;
        }

        // A single-page application owns its own routing, so a path the file server did not answer has to
        // come back as the shell rather than as a 404.
        app.MapFallback(AngularPathBase + "/{**path}", async context =>
        {
            context.Response.ContentType = "text/html";

            await context.Response.SendFileAsync(Path.Combine(angularRoot, "index.html")).ConfigureAwait(false);
        });
    }
}

/// <summary>
/// One order, as the sample API returns it.
/// </summary>
/// <param name="Id">The order number.</param>
/// <param name="Product">What was ordered.</param>
/// <param name="Quantity">How many.</param>
/// <param name="Price">The unit price.</param>
/// <param name="Status">Where the order stands.</param>
public sealed record SampleOrder(string Id, string Product, int Quantity, decimal Price, string Status);
