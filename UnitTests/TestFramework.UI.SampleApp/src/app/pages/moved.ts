import { Component, signal } from '@angular/core';

/**
 * The same button as anywhere else, buried where no selector would look for it: several wrappers deep,
 * last in the document, in a footer, restyled, with generated class names.
 *
 * Nothing about a target says where an element is, so there is nothing here for the layout to break.
 * A CSS-selector test written against the old markup would fail on this page; that contrast is the
 * point of it.
 */
@Component({
  selector: 'app-moved',
  template: `
    <h2>Settings</h2>

    <div class="_ng_9f2c">
      <div class="panel panel--wide">
        <section>
          <div>
            <p>Display name: <input aria-label="Display name" /></p>
          </div>
        </section>
      </div>
    </div>

    <aside class="sidebar">
      <p>Nothing to see here.</p>
    </aside>

    <footer class="footer _x8811">
      <div class="footer__inner">
        <div class="footer__actions">
          <span class="hint">All changes are final.</span>
          <button type="button" class="btn btn--ghost _q71" (click)="saved.set(true)">Save changes</button>
        </div>
      </div>
    </footer>

    @if (saved()) {
      <p data-testid="saved-note">Settings saved</p>
    }
  `,
  styles: `
    .footer { margin-top: 3rem; border-top: 1px solid #ccc; padding-top: 1rem; }
    .footer__actions { display: flex; justify-content: flex-end; gap: 1rem; align-items: center; }
    .btn--ghost { background: transparent; border: 1px solid #333; padding: .4rem .9rem; }
    .sidebar { float: right; width: 8rem; color: #999; }
  `,
})
export class Moved {
  protected readonly saved = signal(false);
}
