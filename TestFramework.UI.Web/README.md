# TestFramework.UI.Web

Points browser steps at the application the **TestFramework.Web** package family already configures,
so one timeline can go in through the UI and verify through the API, the database and the stubs
without describing the application twice.

```csharp
WebAppIdentifier shop = new WebAppIdentifier("shop").FromWebApi("shop-api");

Timeline.Create()
    .Trigger(BrowserExt.Session(shop).Navigate("/orders").Click("Place order"))
    .WaitForEvent(WebExt.Stub.Called("pricing", HttpMethod.Post, "/api/quotes"))
    .FindArtifact("written", WebExt.ArtifactFinder.Sql.Where<Order>("orders-db", "Name = @name")
        .WithParameter("name", Var.Const("Anvil")))
    .Build();
```

`FromWebApi` does two things:

- the browser's base address resolves at run time from the REST API identifier's configuration, so
  there is no second copy of the address to drift, and
- the browser step declares the same environment requirement as the API steps, so an environment that
  provisions the application for `WebExt.Api` satisfies the browser step too - one container, both
  doors.

Add `.LoadUIWebBridge()` to the configuration builder alongside `.LoadUIConfig()` and
`.LoadWebConfig()`.
