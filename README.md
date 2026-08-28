![Icon](https://raw.githubusercontent.com/DeadMoon0/TestFramework-Common/96ef4240c1e55ba95a20b99285219a61407c6355/Assets/Icon.svg)

# TestFramework-UI

`TestFramework.UI.Browser` lets a normal TestFramework timeline drive a web application in a real,
locally running browser — and keeps working when somebody moves, restyles or renames the button.

Elements are addressed the way a person describes them (`Click("Add to cart")`), waiting is the
framework's job rather than the test's, and everything that happened in the browser travels to the
assertions as ordinary timeline variables. The timeline shape, variables, retries, timeouts and
debugging UI are the same ones the rest of the framework uses — the browser is one more door into
the system under test, next to the API, the database and the stubs.

## Choose Your Path

- **The application is already reachable somewhere** (dev server, test stage, a container you
  started yourself): configure `Ui:<identifier>:BaseUrl` and write steps. With Microsoft Edge on the
  machine, set `"Channel": "msedge"` and nothing needs to be downloaded.
- **The application is the one your API serves**, or a site the TestFramework.Web family configures:
  name it under the same identifier and the browser resolves its address from the same configuration
  the API and stub steps use — one identifier, both doors. When the names differ, say so once with
  `BridgedTo(new EnvironmentRequirement(WebEnvironmentResourceKinds.Site, "..."))`.
- **You want the same test as desktop and as phone**: point it at a second configuration entry with
  a `Device` — the browser environment is configuration, never test code.
- **You need the site booted for you**: that is the container lane's job. Browser steps declare what
  they need; an environment that provisions the site under its identifier serves them unchanged.

## Install

```bash
dotnet add package TestFramework.UI.Browser
```

## What It Does

```csharp
Timeline timeline = Timeline.Create()
    .Trigger(BrowserExt.Session("shop")
        .Navigate("/products")
        .Click("Add Anvil to cart")
        .Expect("1 item in cart"))
        .Name("cart")
    .Build();

TimelineRun run = await timeline.SetupRun(config).RunAsync();

run.EnsureRanToCompletion();
run.UiUrl("shop").Should().Contain("/products");
run.UiLooseMatches("shop").Should().HaveNoItems();
```

The last line is the part no other browser tool offers: tolerance with an audit. A renamed control
is still pressed — and the run recorded that it had to reach for it, so a suite can be lenient in
development and strict in the build.

## Source Of Truth

This repository-level README is the landing page. The maintained package docs live with their
packages:

- [TestFramework.UI.Browser/README.md](./TestFramework.UI.Browser/README.md) — driving, reading,
  waiting, structure and layout checks, devices, scripts, and the failure evidence.
- [TestFramework.UI/README.md](./TestFramework.UI/README.md) — the technology-neutral foundation.
- [Documentation/ERROR-HANDLING-UI.md](./Documentation/ERROR-HANDLING-UI.md) — every failure, what
  it captures, and the way out.
- [Documentation/Arc42.md](./Documentation/Arc42.md) — architecture notes.

## Current Scope

Driving (navigate, click, fill, choose, check, press, hover), in-flow expectations that wait,
typed value reads into timeline variables, named JavaScript execution with an audit, wait events
(element, text, address, script, structure, table), structure and table comparison with paste-able
failure output, relational layout checks, geometry and structure drift capture, device profiles,
and automatic failure evidence (screenshot, page markup, session story, console log).

Out of scope today: component testing, in-browser network mocking, pixel visual regression. For a
suite that tests only a browser UI and nothing behind it, a dedicated browser tool with watch mode
and a recorder is the better fit — this package earns its keep when the browser is one door among
several in one timeline.

## Repository Layout

- `TestFramework.UI` — technology-neutral foundation (session picture, comparison algebra, geometry).
- `TestFramework.UI.Browser` — the web-browser implementation, driven through Playwright.
- `UnitTests/` — browser-free tests, the gated browser suite, and the sample application they run
  against.

## Building And Testing

```bash
dotnet build TestFramework.UI.slnx
dotnet test TestFramework.UI.slnx
```

The browser suite runs by itself. It looks for a browser this machine already has — Playwright's own
download first, then an installed Edge or Chrome — and only skips, with its reason, when there is none.
`TESTFRAMEWORK_UI_BROWSER` still selects one (`msedge`, `chromium`, `firefox`) when the default choice
is not what you want; it is no longer what turns the suite on. With no browser at all, run once with
`TESTFRAMEWORK_UI_AUTOINSTALL=1 TESTFRAMEWORK_UI_BROWSER=chromium` to let Playwright download its own.

The browser suite also needs the sample application built once: `npm ci && npm run build` in
`UnitTests/TestFramework.UI.SampleApp`. A fresh clone still goes green on a bare `dotnet test` either
way — what is missing skips visibly rather than failing.
