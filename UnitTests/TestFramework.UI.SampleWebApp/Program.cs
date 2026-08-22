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
    /// Starts the host.
    /// </summary>
    /// <param name="args">Command line arguments, passed to the host builder.</param>
    public static void Main(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
        WebApplication app = builder.Build();

        app.UseDefaultFiles();
        app.UseStaticFiles();

        MapOrdersApi(app);
        MapAngularApp(app);

        // Lets a test fixture confirm the host is up before it drives a browser at it.
        app.MapGet("/health", static () => Results.Ok(new { status = "ok" }));

        app.Run();
    }

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

    private static void MapAngularApp(WebApplication app)
    {
        string angularRoot = Path.GetFullPath(Path.Combine(
            app.Environment.ContentRootPath,
            "..",
            "TestFramework.UI.SampleApp",
            "dist",
            "sample-app",
            "browser"));

        if (!Directory.Exists(angularRoot))
        {
            // The Angular fixture is optional: the hand-written pages carry the suite on a machine
            // without a Node toolchain, and the gate skips the tests that need this one.
            app.MapGet(AngularPathBase, static () => Results.NotFound(
                "The Angular sample application has not been built. Run 'npm ci && npm run build' in " +
                "UnitTests/TestFramework.UI.SampleApp."));

            return;
        }

        PhysicalFileProvider provider = new PhysicalFileProvider(angularRoot);

        app.UseFileServer(new FileServerOptions
        {
            FileProvider = provider,
            RequestPath = AngularPathBase,
            EnableDefaultFiles = true,
        });

        // A single-page application owns its own routing, so anything unresolved under the prefix has
        // to come back as the shell rather than as a 404.
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
