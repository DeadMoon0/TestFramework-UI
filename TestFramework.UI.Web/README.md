# TestFramework.UI.Web

Lets browser steps resolve the application they drive from the **TestFramework.Web** family's
configuration, so one timeline goes in through the UI and verifies through the API, the database and
the stubs without describing the application twice.

## The normal path needs no bridging call

The site is the application a browser loads, so they share one identifier. A browser step that
drives `"shop"` is served by the site configured as `"shop"` — whether a `Site:shop:BaseUrl` entry
names a deployed one, or a container environment started one and published its address:

```csharp
Timeline.Create()
    .Trigger(BrowserExt.Session("shop").Navigate("/orders").Click("Place order"))
    .WaitForEvent(WebExt.Stub.Called("pricing", HttpMethod.Post, "/api/quotes"))
    .FindArtifact("written", WebExt.ArtifactFinder.Sql.Where<Order>("orders-db", "Name = @name")
        .WithParameter("name", Var.Const("Anvil")))
    .Build();
```

```csharp
ConfigInstance config = ConfigInstance.Create()
    .LoadWebConfig()        // Api, Sql, Stub and Site stores
    .LoadUIConfig()         // the Ui section: browser, viewport, timeouts
    .LoadUIWebBridge()      // lets browser steps read the Web stores
    .Build();
```

For that to work, the `Ui:shop` entry carries the browser settings and **no BaseUrl** — an explicit
address is the override and always wins. The address flows through the `Site` store, which is what
makes the environment swap: the same timeline and the same configuration run against a deployed
site, and adding `.SetEnv(DockerWebEnvironment...)` with a matching `DockerSiteDefinition` is the
only change for the containerized run.

## The two explicit tools

`FromWebApi` is for an application the REST API process serves itself — the address belongs to the
API resource, and the browser steps then declare the API's environment requirement, so one
provisioned application serves both doors:

```csharp
WebAppIdentifier shop = new WebAppIdentifier("shop").FromWebApi("shop-api");
```

`FromSite` is only for a name mismatch between the web application and the site:

```csharp
WebAppIdentifier shop = new WebAppIdentifier("shop").FromSite("shop-ui");
```

The configuration-only equivalents are `BaseUrlFromApi` and `BaseUrlFromSite` on the `Ui` entry.
