# TestFramework.UI

The technology-neutral foundation for UI testing inside a TestFramework timeline. It carries no
browser dependency; to drive a web UI, add **TestFramework.UI.Browser**.

What lives here:

- **The session picture** (`UiSessionPicture`, `UiSessionEntry`) - what happened in a UI session,
  travelling from the interactions to the assertions as a variable rather than an artifact. A
  session has no create-and-tear-down lifecycle, so it is data a run accumulates, not a resource it
  owns.
- **Expected-structure comparison** - comparing what a test declared against what a page produced,
  reporting differences as *missing* and *surplus* rather than as a wall of text.
- **Cell rules** (`Cell.Any`, `Cell.Contains`, `Cell.Matches`, `Cell.Satisfies`) - the tolerance
  markers a test places inline in its expected data, so a reviewer sees which parts are pinned and
  which are deliberately loose.
- **Text normalization** (`UiText`) - runs of whitespace collapse and ends trim, even for exact
  comparisons, because a page that wraps a label across two lines is saying the same thing as the
  test.

A future non-web UI technology becomes a sibling package on this foundation.
