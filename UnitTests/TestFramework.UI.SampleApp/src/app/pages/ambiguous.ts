import { MatButtonModule } from '@angular/material/button';
import { Component, signal } from '@angular/core';

/**
 * Three buttons that answer to "Delete", each in a named region, one of them also carrying a test id.
 *
 * A test that just says Delete must fail here rather than pressing one of them, and the failure has to
 * name all three and hand over the line that resolves it.
 */
@Component({
  selector: 'app-ambiguous',
  imports: [MatButtonModule],
  template: `
    <h2>Account</h2>

    <section role="region" aria-label="Saved cards">
      <h3>Saved cards</h3>
      <p>Visa ending 4242</p>
      <button mat-stroked-button type="button" (click)="deleted.set('card')">Delete</button>
    </section>

    <section role="region" aria-label="Addresses">
      <h3>Addresses</h3>
      <p>Teststr. 1</p>
      <button mat-stroked-button type="button" (click)="deleted.set('address')">Delete</button>
    </section>

    <section role="region" aria-label="Account">
      <h3>Danger zone</h3>
      <button mat-stroked-button type="button" data-testid="delete-account" (click)="deleted.set('account')">Delete</button>
    </section>

    @if (deleted()) {
      <p data-testid="deleted-note">Deleted: {{ deleted() }}</p>
    }
  `,
  styles: `
    /* Each region looks like its own place, which is the point of the page: the three buttons are
       genuinely different actions, and a test has to say which one it meant. */
    section {
      padding: 1rem 1.15rem;
      margin: .85rem 0;
      background: var(--mat-sys-surface-container-low);
      border: 1px solid var(--mat-sys-outline-variant);
      border-radius: var(--mat-sys-corner-medium);
    }

    section h3 { margin-bottom: .35rem; }
    section p { color: var(--mat-sys-on-surface-variant); font: var(--mat-sys-body-small); }

    /* The dangerous one says so without renaming its button. */
    section[aria-label='Account'] { border-color: var(--mat-sys-error); }
    section[aria-label='Account'] button { color: var(--mat-sys-error); }
  `,
})
export class Ambiguous {
  protected readonly deleted = signal('');
}
