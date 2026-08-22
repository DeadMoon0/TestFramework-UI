# TestFramework.UI.Browser

Drive a local browser from inside a TestFramework timeline.

```csharp
private static readonly Timeline _timeline = Timeline.Create()
    .Trigger(BrowserExt.Session("shop")
        .Navigate("/products")
        .Click("Anvil")
        .Click("Add to cart"))
        .Name("add-to-cart")
    .WaitForEvent(BrowserExt.Events.TextAppears("shop", "1 item in cart"))
        .WithTimeOut(TimeSpan.FromSeconds(15))
    .Build();
```

## Elements are addressed the way a person describes them

`Click("Add to cart")` looks for something clickable called that - by accessible name, then label,
then placeholder, then test id, then visible text. A control that was restyled, relocated, or renamed
from `Save` to `Save changes` is still found, because none of those is how the test identified it.

Matching runs in two sweeps: everything exact first, then everything loose. An exact match on any
channel therefore beats a loose match on every channel, and a test never silently prefers a guess
over a certainty.

## Loose matches are recorded, never hidden

Every non-exact match lands in the session picture with the channel that matched and how many
elements it found, and `run.UiWeakestMatch("shop")` asserts on it. Tolerance stays useful while a
test is being written, and a build can still refuse to go green on tests that only pass because the
framework guessed well.

When a name genuinely matches several elements, the step fails and lists the candidates together with
the dial that resolves it - `.InSection(...)`, `.Nth(...)`, `.First()` - rather than picking one.

## Precision when you want it

```csharp
.Click(Target.Button("Delete").InSection("Saved cards"))  // kind and region pinned
.Click(Target.TestId("submit-order"))                     // test id
.Click(Target.Css("#legacy-submit"))                      // exact selector, when nothing else fits
```

## Structure is a typed field, not a string

```csharp
private static readonly WebElementStructure OrderList = WebElementStructure
    .OneElement("app-order-list")
        .WithAttribute("name", "orders")
        .Containing(x => x
            .ManyElements("app-order-row").WithText(Cell.Matches(@"A-\d+")));
```

Custom elements are first class, children match as a subset in any order by default, and a mismatch
prints the actual structure as source you can paste back into the test.

## Configuration decides the browser, tests do not

Viewport, device, channel and headlessness live in configuration, so the same timeline runs against a
1080p desktop and an iPhone by pointing at a different identifier - no test change.

## When not to use it

For suites that only test a browser, Playwright's own tooling (UI mode, trace viewer, codegen) is the
better tool. This package exists for timelines where the browser is one door among several, and the
run continues into stubs, databases and files.
