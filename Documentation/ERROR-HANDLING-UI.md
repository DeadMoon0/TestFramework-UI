# Error Handling In TestFramework.UI

The design rule, inherited from the rest of the framework and tightened for the browser: **every
failure must contain its own diagnosis and the next thing to type.** A browser failure additionally
leaves evidence on disk, because the page that produced it is gone by the time anybody reads the
message.

## What Is And Is Not An Error

| Situation | Behaviour |
|---|---|
| A control was renamed, moved or restyled | **Not an error.** The two-sweep resolution still finds it, and records the loose match in the session picture. |
| An element is not there *yet* | **Not an error.** Actions retry resolution, `Expect` waits, the events poll. Only the step timeout ends the waiting. |
| A count of zero (`Value.Count`) | **Not an error.** Zero is an answer. |
| A captured structure or layout changed since the last clean run | **Not an error.** Value-change detection reports it; a capture never fails a run by itself. |
| The application logs console errors while being driven | **Not an error by itself.** They ride along in the session picture, are assertable via `run.UiConsoleErrors(app)`, and are appended to any failure so a crashed page is not misdiagnosed. |
| Several elements answer to a target | `UiAmbiguousTargetException` |
| Nothing answers to a target, even loosely, within the timeout | `UiTargetNotFoundException` |
| An action, read or script failed mid-flow | `UiActionFailedException` wrapping the cause |
| A structure or table comparison never matched | `UiStructureMismatchException` |
| A layout relation never held | `UiLayoutMismatchException` |
| A wait event timed out | `TimeoutException` with the event's own message: what was watched, where the page was, how many polls |
| Missing or unresolvable configuration | `UiConfigurationException` |
| The configured browser is not installed | `UiBrowserNotInstalledException` |
| `WithRetry` on a flow not starting with `Navigate` | `InvalidOperationException` at **plan time**, before any browser exists |
| An asserted value did not match | `ValueAssertionException` from the framework's own assertions |

## The Evidence

A failing step records four widgets per open application into the run's own output, under
`TestFrameworkOutput/<run>/widgets/`, which CI publishes like any other run output — and which the
debugging tool draws, the run's summary lists, and a shared run bundle carries:

- `<step>-<app>.png` — the page as it was at the failure
- `<step>-<app>-page.html` — the markup, for reading what the locators saw
- `<step>-<app>-session.json` — everything the session did up to that point
- `<step>-<app>-console.txt` — what the application itself complained about, when it complained

Each is attributed to the step and the attempt that produced it, so a retry does not photograph over
the first failure — which is usually the interesting one. The run states the attempt itself; nothing
here has to name it.

Set `WidgetCapture: EveryAction` on an application to photograph after every action rather than only
on a failure. A screenshot a test asks for by name with `.Screenshot("...")` is always kept, whatever
that setting says.

### Asking for a picture of now

A run stopped at a breakpoint is sitting on a page nothing has photographed: the step that navigated
there has not finished, so the newest picture predates it. The debugging tool offers a button while a
run is held, and this package answers it by photographing every open application — one widget named
`live-<app>`, so repeated asks read as versions of one page in the order they were made.

Two cases where it declines rather than hangs, both said out loud in the run's log so the reason
reaches whoever pressed the button:

- **Another step is driving the page.** The capture waits five seconds for the page to be free and
  then gives up. A missing picture is cheap; a hung debugger asking for one is not.
- **The browser is being held open for a person** (`PauseOnFailure`). A page stopped in Playwright's
  inspector does not answer a screenshot request — it waits for the same person the hold is for.

This used to be a folder of this package's own, named after a timestamp and a fresh identifier — which
shared no key with the run that produced it, so the evidence existed and nothing could find it.

Writing it is not the step's job. The engine tells whoever is watching a run that a step failed or ran
out of time, and this package registers one such observer — `.LoadUIConfig()` does it, so a timeline that
configures the browser has the evidence too. The step names the folder in its own message; the observer
fills it, and says in the run log where it went or why it could not.

Locally, `TESTFRAMEWORK_UI_PAUSE_ON_FAILURE=1` also holds the browser open at the failure state for as
long as you need it — the run says out loud that it is being held — and `TESTFRAMEWORK_UI_HEADED=1` /
`TESTFRAMEWORK_UI_SLOWMO=250` replay a run watchably. These are environment overrides, never
configuration — they belong to a person's machine, not to a suite.

## UiTargetNotFoundException

Nothing answered to the target on any channel, exact or loose, within the timeout. The message lists
which lookups were tried, where the page was, and — the fast fix — the accessible names of that kind
the page *does* offer:

```
No element matching 'Emial' was found on 'shop' at http://localhost:5090/app/checkout.
Tried, in order: label (exact), placeholder (exact), role=textbox (exact), ...
The page offers fields named: 'Email', 'Street', 'City'.
```

Recovery: it is almost always a typo or an outdated wording — take the name from the offered list.
If the element genuinely appears later than the step timeout allows, raise `.WithTimeOut(...)` on
the step, or put a wait event in front where another actor produces it.

## UiAmbiguousTargetException

Several elements answered, and acting on one of them would be a guess. The message lists every
candidate and the dial that resolves it:

```
The 'Delete' matches 3 elements on 'shop', so the run will not guess which one it is.
Candidates:
  1. <button> "Delete" in region 'Saved cards'
  2. <button> "Delete" in region 'Addresses'
  3. <button data-testid="delete-account">
Pick one: .InSection("Saved cards"), .Nth(0), Target.TestId("delete-account"), or .First() to
accept the first on purpose.
```

Recovery: copy one of the offered dials. Ambiguity is a real answer about the page, so it is never
retried away — but note that a closing overlay can be a *transient* duplicate; the interaction verbs
already wait those out where they cause them (`Choose` waits for its popup to close).

## UiActionFailedException

An action inside a flow failed: the wrapped cause carries what went wrong, and the wrapper carries
where — which action of how many, what already worked, the page address, the application's console
errors during the step, and the failure bundle path:

```
Action 4 of 6 on 'shop' failed: Fill 'Email'.
Everything before it worked:
  Navigate -> .../checkout [419 ms]
  Click 'Checkout' via RoleExact [81 ms]
  ...
```

Recovery: read the wrapped cause first — it is the diagnosis; the wrapper tells you how far the
flow got. If the cause is the application (console errors listed), fix the application, not the
test.

## UiStructureMismatchException

A structure or table comparison still differed when the settle time ran out. The message lists every
difference with its path (`row 1, column 'Qty': expected is '7', found '1'`), shows what the page
actually was, and hands back the expectation that *would* have passed — as the same C# the test is
written in, ready to read against the intended one. Recovery: if the page is right, paste the
suggestion; if the page is wrong, the difference list is the bug report.

## UiLayoutMismatchException

A layout relation still did not hold when the settle time ran out. Every violated relation is listed
with both actual rectangles, plus a table of where everything measured is:

```
expected button 'Place order' above field 'Email', but button 'Place order' is at
(529, 817, 121×40) and field 'Email' is at (545, 196, 430×24)
```

Recovery: the rectangles are the page's answer — compare them against the intent. An element listed
as "not on the page, or has no size to measure" is hidden or absent; check visibility before
geometry.

## UiConfigurationException

An identifier has no configuration, or no road to an address. The message lists the identifiers that
*are* registered, and — with the bridge — which stores were asked. Recovery: add the
`Ui:<identifier>` entry, or load the Web configuration (`.LoadWebConfig()`) so a `Site:`/`Api:` entry can
answer, or let the environment publish it.

## UiBrowserNotInstalledException

The configured browser engine is not on the machine. The message names the three ways out: set
`"Channel": "msedge"` to use the installed Edge with no download, call
`BrowserExt.Tooling.InstallBrowsers("chromium")` once from a fixture (CI opts in with
`TESTFRAMEWORK_UI_AUTOINSTALL=1`), or run Playwright's install command by hand.

## Wait Event Timeouts

A wait event raises `TimeoutException` with its own message — what was watched, where the page was,
how many polls, the advice for that kind of wait, and the bundle path. Structure and table waits
additionally report their **last look**, difference by difference, with the expectation the page did
satisfy at that moment. The attribute waits report the value the attribute actually read on the last
look (`AttributeEquals`) or the baseline it never moved from (`AttributeChanged` — whose baseline is
the wait's *first* look, so a change that happens before the wait starts is invisible to it). The
count waits report the last count. The event gives up slightly before its step timeout on purpose: the runner
abandons a timed-out step the instant its own clock fires, and a message raised at that same moment
would never be seen.

## The Plan-Time Refusals

Two mistakes are refused before any browser exists, because discovering them in a run would be
discovering them in production data:

- `WithRetry` on a flow whose first action is not `Navigate` (`InvalidOperationException` naming the
  rule and the fix) — a retried attempt replays actions against whatever the failed one left behind.
- A mistyped variable in any step input (`IOContractViolationException` from the Core contract
  validation) — the plan knows every declared input and output.
