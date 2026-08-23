import { Component } from '@angular/core';

/**
 * A page taller than any viewport: a changelog of eighty entries, a note at the top, a note at the
 * bottom. Nothing here waits or hides - the fixture exists so a test can prove that the scroll verbs
 * actually moved the viewport, which the layout checks then pin with InViewport.
 *
 * Visiting it also plants a cookie, because a changelog is the kind of page that remembers it was
 * read - and a test needs one honest cookie to read back.
 */
@Component({
  selector: 'app-scrolling',
  template: `
    <h2>Changelog</h2>

    <p data-testid="top-note">The newest entry is at the top.</p>

    @for (entry of entries; track entry) {
      <p class="entry">Entry {{ entry }}: routine maintenance and small fixes.</p>
    }

    <p data-testid="end-note">You reached the end of the changelog</p>
  `,
  styles: `
    .entry {
      max-width: 40rem;
      padding: .55rem .9rem;
      margin: .35rem 0;
      background: var(--mat-sys-surface-container);
      border-radius: var(--mat-sys-corner-medium);
      font: var(--mat-sys-body-medium);
    }
  `,
})
export class Scrolling {
  protected readonly entries = Array.from({ length: 80 }, (_, index) => 80 - index);

  constructor() {
    document.cookie = 'changelog-visited=yes; path=/';
  }
}
