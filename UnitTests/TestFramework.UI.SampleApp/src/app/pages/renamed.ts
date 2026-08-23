import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { Component, signal } from '@angular/core';

/**
 * The control a test called "Save" is now called "Save changes".
 *
 * A test written against the old wording must still press it, and the run must record that it matched
 * loosely - so a suite that wants to be strict can still notice.
 */
@Component({
  selector: 'app-renamed',
  imports: [MatButtonModule, MatFormFieldModule, MatInputModule],
  template: `
    <h2>Settings</h2>

    <mat-form-field appearance="outline">
      <mat-label>Display name</mat-label>
      <input matInput aria-label="Display name" />
    </mat-form-field>

    <div><button mat-flat-button type="button" (click)="saved.set(true)">Save changes</button></div>

    @if (saved()) {
      <p data-testid="saved-note">Settings saved</p>
    }
  `,
  styles: `
    mat-form-field { width: 100%; max-width: 22rem; display: block; margin-bottom: .5rem; }
  `,
})
export class Renamed {
  protected readonly saved = signal(false);
}
