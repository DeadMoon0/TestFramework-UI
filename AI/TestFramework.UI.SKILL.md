<identity>
    <package>TestFramework.UI.Browser</package>
    <role>addon-skill</role>
</identity>

<objective>
    Explain how a TestFramework timeline drives a web application in a real browser through TestFramework.UI.Browser: how elements are addressed by what a person sees, how waiting works, how what happened reaches the assertions, how structure and layout are compared, and why every tolerance the framework grants is recorded where a suite can audit it.
</objective>

<package_scope>
    Covers BrowserExt.Session(...) interaction flows, BrowserExt.Page(...) inspections (structure, table, layout, captures), BrowserExt.Events waits, the Target model and two-sweep resolution, typed value reads (Value.*), named JavaScript execution (Js.*), device profiles and configuration, the session picture, and the failure evidence.
    Also covers TestFramework.UI.Web: resolving the application's address from the TestFramework.Web family's Site and Api configuration.
    Does not cover starting or hosting the application, its database or its stubs; browser steps declare requirements (ui.webapp, or the bridged kind) and an environment satisfies them.
</package_scope>

<key_concepts>
    The flow IS the step: BrowserExt.Session("shop").Navigate("/products").Click("Anvil") is a Step with no terminal verb. Verbs may be added until the timeline is built.
    Elements are addressed by what a person sees. A plain string is a smart target whose meaning comes from the verb: Click looks for something clickable, Fill for a field, Expect for text. Target.Button/Link/Field/Checkbox/Radio/Text/Role/Section pin the kind; Target.TestId/Css/Id are the technical escape hatch, ranked last on purpose.
    Resolution runs in two sweeps: every channel exact first (role+name, label, placeholder, test id, text - whitespace always normalized), then every channel loose (case-insensitive substring). An exact match on any channel beats a loose match on every channel.
    Ambiguity never acts. Several matches fail listing every candidate and the dial that resolves it: .InSection(...), .Nth(n), .Near(...), Target.TestId(...), or .First() to accept the first deliberately. Not-found failures list the names the page does offer.
    Every loose match is recorded in the session picture with its channel and candidate count. run.UiLooseMatches(app) and run.UiWeakestMatch(app) make tolerance auditable; a suite asserts them empty to require exactness without knowing which channel matched what.
    What happened in the browser travels to the assertions as VARIABLES, never artifacts - a session has no create-and-teardown lifecycle. The per-app session picture (ui:<app>) accumulates every action, wait, script and screenshot path; Read(...) writes real timeline variables later steps consume with Var.Ref.
    Every UI step on one application declares the session variable, which orders same-app steps for the planner without DoNotParallelize; different applications still parallelize.
    Waiting is the framework's job. Actions auto-wait for actionability; Expect is a retried query and a sync point; ExpectNot waits for disappearance rather than checking once. Between steps, BrowserExt.Events.* wait for what another actor produces, give up slightly before the step timeout so their own message survives, and record themselves into the session story. Fill sets a value in one motion; Type presses real keys for the pages that listen to keystrokes. Scrolling and the pointer verbs (Hover, MouseAway, DragTo, DoubleClick, RightClick) are first-party actions in the trace, never scripts.
    Reads are snapshots with one natural type each: text and values are strings, Checked is a bool, Count is an int (zero is an answer, not a failure). Richer parsing is the test's own Transform - the framework never guesses a culture.
    Choose drives both list contracts - a native select (label first, then value) and the ARIA combobox (role=combobox opening a role=listbox of role=option) - and is not done until the popup closed. Select stays native-only and its failure names Choose.
    JavaScript is a named value (Js.Inline/Js.FromFile), argument-fed from timeline variables (declared step inputs, arriving as one args object of strings), typed on the way out (Evaluate&lt;T&gt;), and audited: every Execute, Evaluate and ScriptIsTrue lands in the session picture and run.UiScripts(app) counts them. A script bypasses actionability - it is for reading and seeding state, not for acting.
    Structure is typed code, not a snapshot string: WebElementStructure declares element kinds (component names like app-order-row included), per-node rules and cardinalities; ExpectedTable declares rows matched unordered by a key column, with missing/surplus reporting and Cell.* tolerance markers inline. Both compare against a normalized projection that never contains class, style or framework bookkeeping, are retried until the page settles, and fail with paste-able output: the actual state rendered as the same C# the test is written in.
    Layout is relations, not coordinates: ExpectedLayout states Above/LeftOf/Inside/NotOverlapping/InViewport/NoHorizontalScroll with a two-pixel tolerance. All named elements are measured in one moment; a failure lists every violated relation with both actual rectangles.
    Drift is watched, not asserted: CaptureStructure and CaptureLayout store normalized projections in variables, and the framework's value-change detection reports what changed against the last clean run. A capture never fails a run by itself.
    The browser environment is configuration, never test code: browser, channel, headless, and a Device profile (Playwright descriptors like "iPhone 14", or the package presets Desktop 720p/1080p/1440p, Laptop, Tablet, Narrow). BasedOn inherits an entry and overrides selectively, so the phone variant of an application is one line. The same timeline runs on both by naming the other identifier.
    A retried flow replays its actions against whatever the failed attempt left behind, so WithRetry on a flow is refused at plan time unless its first action is Navigate.
    Every failure carries its own diagnosis and the next thing to type, plus an evidence bundle on disk: screenshot, page markup, session story and console log, in the run's output folder that CI publishes. Console errors and page exceptions ride along in the session picture, so a crashed application is never misdiagnosed as a locator problem.
    Sessions are pooled browsers with per-run contexts: one run never sees another run's cookies or storage, and the first browser step of a run claims the one cleanup step that closes its sessions.
    With TestFramework.UI.Web, an application configured or provisioned as a Site under its own identifier resolves with no bridging call at all; FromWebApi(apiIdentifier) covers the application its API process serves, and carries that requirement so one provisioned container serves API steps and browser steps alike.
</key_concepts>

<best_practices>
    Address elements by their words: Click("Add Anvil to cart"), Fill("Email", ...). Reach for Target.* kinds when the words are ambiguous, and Target.TestId/Css only when nothing user-facing identifies the element.
    Assert run.UiLooseMatches(app).Should().HaveNoItems() in suites that want to be strict; leave it off while a page is being reworked, and the trace still shows every reach.
    Keep assertions in-house: run.UiUrl/UiTitle/UiTrace/UiSession/UiScreenshot/UiConsoleErrors, run.UiTable/UiColumn/UiDifferences, run.Variable&lt;T&gt;(...) - all ValueHandle-based, signalled to the debugging UI.
    Use Expect inside a flow for "the page caught up with me", Events between steps for "another actor got the page here", and never a sleep anywhere.
    Give every step a Name; the session picture, the failure bundle folder and the assertions all key on it.
    Prefer Choose over Select; it drives native lists and comboboxes alike and matches by what a person sees.
    Declare structures, tables and layouts as static readonly fields beside the timeline; they freeze on first use and are shared safely.
    State only what the test is about: subset matching, unordered rows and layout relations exist so somebody adding a column or moving a panel does not break a test about something else. Escalate locally (ContainingExactly, InOrder, InDocumentOrder) only where the point IS the order.
    Read values into variables and let later steps consume them with Var.Ref; parse with Transform where a type is needed.
    Keep scripts rare, named, and honest: seed and read state, never click through JavaScript. Watch run.UiScripts(app) the way you watch loose matches.
    Put WithRetry only on flows that start with Navigate; the plan-time guard enforces it, so design for it.
    Set "Channel": "msedge" on Windows machines and CI where Edge is present - zero download; use TESTFRAMEWORK_UI_AUTOINSTALL=1 with chromium on Linux runners.
    Diagnose locally with the environment overrides: TESTFRAMEWORK_UI_HEADED=1 to watch, TESTFRAMEWORK_UI_SLOWMO=250 to follow, TESTFRAMEWORK_UI_PAUSE_ON_FAILURE=1 to inspect the live page at the failure.
    Data-driven browser tests currently use a loop inside a [Fact]; the Core test-identity resolver crashes under xunit theory invokers until the Core fix ships.
</best_practices>

<api_hints>
    Important APIs and shapes from the package:
    - BrowserExt.Session(app).Navigate(path).Click(target).DoubleClick(target).RightClick(target).Fill(target, value).FillSensitive(...).Type(target, text).Select(...).Choose(target, option).Check|Uncheck(target).Press(keys).Press(target, keys).Hover(target).MouseAway().DragTo(target, destination).ScrollTo(target).ScrollToTop().ScrollToBottom().Expect(target).ExpectNot(target).Read(target|Value.*, into).Screenshot(name?).Execute(js).Execute(target, js).Evaluate&lt;T&gt;(js, into).Evaluate&lt;T&gt;(target, js, into)
    - Target.Button|Link|Field|Checkbox|Radio|Text|Role|Section(name), Target.TestId|Css|Id(...); dials: .ExactMatch(), .Nth(n), .InSection(heading), .Within(target), .Near(text), .First(); plain strings convert implicitly
    - Value.Text|FieldValue|Checked|Count|Attribute|SelectedOption|Style(target, ...), Value.Url(), Value.QueryParam(name), Value.LocalStorage(key), Value.Cookie(name) - the browser's jar, HttpOnly included
    - Js.Inline(source) | Js.FromFile(path), .Named(name), .WithArgument(name, variable)
    - BrowserExt.Page(app).CompareStructure(scope, WebElementStructure) | CompareTable(table, ExpectedTable) | CheckLayout(ExpectedLayout) | ReadTable(table, into?) | CaptureStructure(scope, name) | CaptureLayout(scope, name) | Screenshot(name?)
    - WebElementStructure.OneElement(tag).WithAttribute(name, value|rule|transform).WithText(value|rule).Where(predicate, description).Containing(x => { x.ManyElements(tag); x.OneElement(tag); }).ContainingExactly(...).InDocumentOrder(); children: OneElement, ManyElements, OptionalElement, Exactly(n, tag), AtLeast(n, tag), NoElement
    - ExpectedTable.WithHeader(columns...).Row(cells...).KeyedBy(column).AllowExtraRows().InOrder(); cells: strings or Cell.Any|Contains|Matches|Satisfies|NotEmpty
    - ExpectedLayout.Above|LeftOf|Inside|NotOverlapping|InViewport|NoHorizontalScroll(...) chained with And*
    - BrowserExt.Events.ElementVisible|ElementHidden(app, target), TextAppears|TextDisappears(app, text), AttributeEquals(app, target, attribute, rule), AttributeChanged(app, target, attribute) - baseline is the wait's first look, CountIs|CountAtLeast(app, target, n), UrlMatches(app, pattern).AsRegex(), ScriptIsTrue(app, js), StructureMatches(app, scope, structure), TableMatches(app, table, expected) - all take an optional pollDelay, default 500 ms
    - run.UiSession|UiUrl|UiTitle|UiTrace|UiLooseMatches|UiWeakestMatch|UiScripts|UiScreenshot|UiConsoleErrors(app), run.UiTable|UiColumn|UiDifferences|UiActualStructure|UiCapturedStructure(label), run.Variable&lt;T&gt;(name)
    - run.Step(label).UiResult() | UiCompare() | UiTableResult() | UiCapture() for the raw typed results
    - WebAppConfig: BaseUrl, Browser, Channel, Headless, Device, BasedOn, ViewportWidth/Height, UserAgent, IsMobile, HasTouch, DeviceScaleFactor, Locale, ColorScheme, SlowMo, DefaultActionTimeout, DefaultCompareTimeout, TestIdAttribute, AmbiguityMode, IgnoreHttpsErrors, BaseUrlFromSite, BaseUrlFromApi
    - Exceptions: UiConfigurationException, UiTargetNotFoundException, UiAmbiguousTargetException, UiActionFailedException, UiStructureMismatchException, UiLayoutMismatchException, UiBrowserNotInstalledException
    - Extension points: IUIComponentFactory (browser/session lifetime), IUiBaseUrlSource (address bridging)
    - TestFramework.UI.Web: identifier.FromWebApi(apiId) | FromSite(siteId), .LoadUIWebBridge() on the config builder, services.AddUiWebBridge() for hand-built services
    - BrowserExt.Tooling.InstallBrowsers("chromium") - a fixture helper, deliberately not a step
</api_hints>

<configuration_shape>
    {
      "Ui": {
        "shop": {
          "BaseUrl": "http://localhost:5090/",
          "Channel": "msedge",
          "Headless": true,
          "Device": "Desktop 1080p",
          "DefaultActionTimeout": "00:00:10"
        },
        "shop-mobile": { "BasedOn": "shop", "Device": "iPhone 14" },
        "shop-via-api": { "BaseUrlFromApi": "shop-api" }
      }
    }
</configuration_shape>

<anti_patterns>
    Do not address elements by CSS or XPath as the primary channel; the words a person sees are the identity, the selector is the escape hatch.
    Do not add sleeps anywhere; actions auto-wait, Expect retries, and the events cover waiting between steps.
    Do not check absence once; ExpectNot and ElementHidden wait for it, because a thing that has not appeared yet is absent too.
    Do not resolve ambiguity with .First() as a habit; it is a deliberate, recorded choice, not a default.
    Do not put WithRetry on a flow that does not start with Navigate; the plan refuses it, because a replayed "Place order" places a second order.
    Do not act through JavaScript; a script that clicks bypasses everything the verbs guarantee and can green-light a page a person could not use.
    Do not assert coordinates or pin computed styles as a habit; layout is relations, and Value.Style is for the property that IS the requirement.
    Do not write a structure that names everything; state what the test is about and let subset matching absorb the rest.
    Do not uppercase text with CSS on pages under test; the framework reads rendered text, and "PLACE ORDER" is not "Place order".
    Do not put addresses in timelines; identifiers resolve through configuration, and with the bridge, through the Web family's configuration.
    Do not treat a mat-select as a native select; Select refuses it and Choose drives it.
    Do not model what happened in the browser as an artifact; it has no lifecycle, and the session picture and read variables already carry it.
</anti_patterns>

<grounding_files>
    - TestFramework.UI.Browser/README.md
    - TestFramework.UI.Web/README.md
    - TestFramework.UI/README.md
    - Documentation/ERROR-HANDLING-UI.md
    - Documentation/Arc42.md
</grounding_files>

<sources>
    - TestFramework.UI.Browser/BrowserExt.cs
    - TestFramework.UI.Browser/Steps/UiBrowserFlow.cs
    - TestFramework.UI.Browser/Targeting/Target.cs
    - TestFramework.UI.Browser/Resolution/TargetResolver.cs
    - TestFramework.UI.Browser/Reading/UiValueSource.cs
    - TestFramework.UI.Browser/Scripting/JsScript.cs
    - TestFramework.UI.Browser/Structure/WebElementStructure.cs
    - TestFramework.UI.Browser/Layouting/ExpectedLayout.cs
    - TestFramework.UI.Browser/Events/UiEvent.cs
    - TestFramework.UI.Browser/Configuration/WebAppConfig.cs
    - TestFramework.UI.Browser/BrowserTimelineResultExtensions.cs
    - TestFramework.UI/Session/UiSessionPicture.cs
    - TestFramework.UI/Structure/ExpectedTable.cs
    - TestFramework.UI/Layout/UiBox.cs
    - TestFramework.UI.Web/WebAppIdentifierExtensions.cs
</sources>
