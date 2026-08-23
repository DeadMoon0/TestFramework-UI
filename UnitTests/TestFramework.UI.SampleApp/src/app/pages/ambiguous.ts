import { Component, signal } from '@angular/core';

/**
 * Three buttons that answer to "Delete", each in a named region, one of them also carrying a test id.
 *
 * A test that just says Delete must fail here rather than pressing one of them, and the failure has to
 * name all three and hand over the line that resolves it.
 */
@Component({
  selector: 'app-ambiguous',
  template: `
    <h2>Account</h2>

    <section role="region" aria-label="Saved cards">
      <h3>Saved cards</h3>
      <p>Visa ending 4242</p>
      <button type="button" (click)="deleted.set('card')">Delete</button>
    </section>

    <section role="region" aria-label="Addresses">
      <h3>Addresses</h3>
      <p>Teststr. 1</p>
      <button type="button" (click)="deleted.set('address')">Delete</button>
    </section>

    <section role="region" aria-label="Account">
      <h3>Danger zone</h3>
      <button type="button" data-testid="delete-account" (click)="deleted.set('account')">Delete</button>
    </section>

    @if (deleted()) {
      <p data-testid="deleted-note">Deleted: {{ deleted() }}</p>
    }
  `,
  styles: `
    section { border: 1px solid #ddd; padding: .5rem 1rem; margin: .8rem 0; }
  `,
})
export class Ambiguous {
  protected readonly deleted = signal('');
}
