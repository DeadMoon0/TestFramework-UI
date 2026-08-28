# TestFramework-UI - Architecture Notes

Structured loosely along arc42. Short on purpose: the code carries the detailed reasoning in its
documentation comments, and the README files carry the usage story. This file holds what neither
does — the shape of the whole and the decisions that made it.

## 1. Introduction And Goals

Drive a web application in a real, locally running browser from an ordinary TestFramework timeline.
Three goals rank above everything else:

1. **Resilience with an audit.** A test that says `Click("Save")` must survive the button being
   moved, restyled or renamed to "Save changes" — and the run must record every such reach, so
   tolerance never becomes silent drift.
2. **One timeline, several doors.** The browser is one way into the system under test, beside the
   API, the database and the stubs. The same timeline shape, variables, assertions and debugging.
3. **Failures that fix themselves in the message.** Candidate lists, "the page offers" name lists,
   paste-able expectation output, actual rectangles, and an evidence bundle on disk.

## 2. Constraints

- Repo conventions of the family: `net8.0;net10.0`, `ImplicitUsings` disabled, `Nullable` enabled,
  warnings are errors, `PackageReference` to *published* family packages (never `ProjectReference`
  across repos).
- Step results and everything in the session picture must be plain serializable data: results travel
  to the debugging UI over a pipe.
- A fresh clone must go green on a bare `dotnet test`, and the browser tests decide that by looking for
  a browser rather than waiting to be told about one. `TESTFRAMEWORK_UI_BROWSER` selects which; it does
  not decide whether. Nothing found is a visible skip that names what is missing.
- A step reads its own deadline from its `RunContext` and can ask whether the time ran out. The engine
  cancels at the deadline and then waits a grace window in which the step's own account is what
  surfaces, so nothing here under-cuts its budget to be heard. The own-deadline pattern this document
  used to prescribe is gone, and `UiEventDeadline` with it.
- Turning this package on is one call. `AddUiBrowser(...)` registers the applications and everything
  that drives them together, `.LoadUIConfig()` reaches the same place, and the store behind them is
  internal so half of it cannot be registered alone.

## 3. Context

Consumers write xunit tests using the Core timeline DSL. This repo adds the browser door. Upstream:
TestFramework.Core (timeline model), TestFramework.Config (configuration builder), Microsoft
Playwright (browser automation as a driver, deliberately *not* as a test runner), and — only in the
bridge package — TestFramework.Web (Site and Api configuration stores). Downstream: a container lane
can provision sites and publish their addresses into the same stores the bridge reads.

## 4. Solution Strategy

Wrap Playwright's driver, not its test model: locators, auto-waiting and device descriptors are
used; runner, fixtures, tracing and assertions are the framework's own. Everything user-facing is
declarative and freezes on first use (flows, structures, tables, layouts). Everything the browser
did travels to assertions as variables. Every tolerance the framework grants is recorded where a
suite can assert on it.

## 5. Building Block View

Three packages, deliberately layered:

- **TestFramework.UI** — technology-neutral foundation, no Playwright: the session picture
  (`UiSessionPicture`), the comparison algebra (`StructureDiffer`, `ExpectedTable`, `Cell`), text
  normalization (`UiText`) and geometry (`UiBox`, `UiBoxRelations`) - public
  on purpose, because a bridge is how another package joins in and no package may need private access to
  do it.
  A future desktop package builds on this without dragging a browser along.
- **TestFramework.UI.Browser** — the web implementation: `BrowserExt` facade (`Session`, `Page`,
  `Events`, `Tooling`), the flow step (`UiBrowserFlow` — the flow *is* the step), the two-sweep
  `TargetResolver` over `IUiElementQuery`, typed reads (`Value.*`), named scripts (`Js.*`),
  inspections (structure, table, layout, captures), wait events, device profiles, and the
  Playwright runtime (`PlaywrightHost` process pool → per-run contexts → per-app sessions).
There is no package between `.Browser` and the Web family. A browser step asks the run where its
application is, and whatever configured or started that application published the address there; a
name mismatch is said once on the identifier with `BridgedTo`, naming the kind the serving package
defines. A `TestFramework.UI.Web` once held two helpers for that and was removed when bridging
became one public operation — the introduction it performed is no longer needed.

Shared targeting abstractions stay in `.Browser` until a second UI technology exists — rule of
three, not speculation.

## 6. Runtime View

A flow step executes as: resolve config (+ environment overrides) → get or create the app's session
(pooled browser, per-run context, per-app page behind a semaphore) → per action: resolve target
(two sweeps, retrying not-found within the action timeout), act with Playwright's actionability,
append a session-picture entry → write the picture variable. Inspections and events run the same
skeleton with a settle loop (retry-until-matched within the compare timeout) and per-poll gate
acquisition. The first browser step of a run claims the single cleanup step that closes the run's
contexts; pooled browsers outlive runs like pooled HTTP clients.

## 7. Deployment View

A NuGet package consumed by test projects. Browser binaries come from the machine (Edge via
`Channel`) or a one-time Playwright install; nothing downloads implicitly. CI runs the unit lane
always and the browser lane behind the gate, target frameworks sequentially
(`TestTfmsInParallel=false`) because two browser fleets on one runner make real-seconds waits miss
their windows. Failure bundles and screenshots land in the run output folder CI already publishes.

## 8. Crosscutting Concepts

- **Two-sweep resolution**: exact on every channel before loose on any; ambiguity always loud;
  test-id ranked last; whitespace always normalized.
- **Session picture as ordering spine**: every UI step of an app declares the `ui:<app>` variable,
  so the planner orders same-app steps and parallelizes different apps.
- **Settle loops everywhere a page is compared**: one-shot comparison against a live page is the
  canonical flake.
- **Own-deadline timeouts** for everything with a story to tell.
- **Audits over prohibitions**: loose matches (`UiLooseMatches`), script use (`UiScripts`) — the
  framework permits and records, the suite decides.
- **Invariant culture in every message**: a failure must read identically on every machine.

## 9. Decisions

- **The flow is the step; no terminal verb.** Mutate-until-frozen, the family's own fluent pattern.
- **Imperative verbs** (`Navigate`, `Click`), not narrative ones — explicitly chosen over an
  actor-style DSL.
- **Session data is variables, never artifacts** — no lifecycle, no deconstruction; screenshots are
  files plus paths in the picture.
- **Structure is typed C#, not snapshot strings**, and failure output *is* the record format.
- **Layout is relations, not coordinates**, with one shared 2px tolerance. Appearance heuristics
  (contrast, focus rings) were deliberately not built: they would rest on approximations.
- **`Select` stays native-only; `Choose` drives both list contracts** and is not done until the
  popup closed (the closing overlay is otherwise a transient ambiguity for the next lookup).
- **Retries only on Navigate-first flows**, refused at plan time.
- **Playwright is an implementation detail** behind `IUiElementQuery` and `IUIComponentFactory`;
  the resolver composes its locators and never re-implements text matching.

## 10. Quality Requirements

The `Commitments` idea from the plan is realized as the test suite itself: resilience proven on
mutation pages (renamed, moved, restyled, ambiguous, delayed, broken), the paste-back round trip
compiled and re-run, device profiles proven on the responsive page, isolation proven across runs,
evidence bundles asserted on disk, and every failure-message contract asserted verbatim. Docs are
under test: skill grounding files must exist, README samples mirror real tests, and every public
exception type must appear in ERROR-HANDLING-UI.md.

## 11. Risks And Technical Debt

- The resolver's channel ladders are owned code on top of Playwright's locators; shadow DOM and
  iframes defer to Playwright semantics and are not first-class yet (iframes: planned as a `Within`
  variant).
- Browser suites are load-sensitive; the sequential-TFM setting mitigates on one machine, but a
  crowded CI host can still stretch the delayed-page timings.
- The Core test-identity resolver crashes under xunit theory invokers (fix tracked in Core); until
  it ships, data-driven browser tests loop inside a `[Fact]`.
- Component testing, in-browser network mocking and pixel comparison are out of scope; teams needing
  them combine this package with dedicated tools.

## 12. Glossary

- **Session picture** — the per-application variable accumulating everything a UI session did.
- **Two-sweep resolution** — exact on all channels, then loose on all channels.
- **Loose match** — a match found below the first exact rung; recorded, assertable.
- **Settle loop** — retry-until-matched within the compare timeout, reporting the last look.
- **Failure bundle** — screenshot, page markup, session story and console log for one failure.
- **Device profile** — a named viewport/input identity (`Desktop 1080p`, `iPhone 14`), configuration
  only.
- **Bridge** — resolving a browser identifier's address from the Web family's configuration.
