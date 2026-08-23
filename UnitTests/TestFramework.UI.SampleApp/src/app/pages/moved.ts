import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
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
  imports: [MatButtonModule, MatFormFieldModule, MatInputModule],
  template: `
    <h2>Settings</h2>

    <div class="_ng_9f2c">
      <div class="panel panel--wide">
        <section>
          <div>
            <mat-form-field appearance="outline">
              <mat-label>Display name</mat-label>
              <input matInput aria-label="Display name" />
            </mat-form-field>
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
          <button mat-stroked-button type="button" class="btn btn--ghost _q71" (click)="saved.set(true)">Save changes</button>
        </div>
      </div>
    </footer>

    @if (saved()) {
      <p data-testid="saved-note">Settings saved</p>
    }
  `,
  styles: `
    /* The markup here is deliberately unhelpful - wrappers, a float, generated-looking class names -
       because that is this page's job. It is styled only enough to look like a settings screen, and the
       button still ends up a long way from where any selector would look for it. */
    .panel {
      padding: 1.15rem 1.35rem;
      background: var(--mat-sys-surface-container-low);
      border: 1px solid var(--mat-sys-outline-variant);
      border-radius: var(--mat-sys-corner-medium);
    }

    mat-form-field { width: 100%; max-width: 22rem; }
    .sidebar { float: right; width: 9rem; color: var(--mat-sys-on-surface-variant); font: var(--mat-sys-body-small); }
    .footer { margin-top: 3rem; border-top: 1px solid var(--mat-sys-outline-variant); padding-top: 1rem; clear: both; }
    .footer__actions { display: flex; justify-content: flex-end; gap: 1rem; align-items: center; }
    .hint { color: var(--mat-sys-on-surface-variant); font: var(--mat-sys-body-small); }
  `,
})
export class Moved {
  protected readonly saved = signal(false);
}
