import { Component, signal } from '@angular/core';

/**
 * The control a test called "Save" is now called "Save changes".
 *
 * A test written against the old wording must still press it, and the run must record that it matched
 * loosely - so a suite that wants to be strict can still notice.
 */
@Component({
  selector: 'app-renamed',
  template: `
    <h2>Settings</h2>

    <p>Display name: <input aria-label="Display name" /></p>

    <button type="button" (click)="saved.set(true)">Save changes</button>

    @if (saved()) {
      <p data-testid="saved-note">Settings saved</p>
    }
  `,
})
export class Renamed {
  protected readonly saved = signal(false);
}
