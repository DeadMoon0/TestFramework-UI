import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatRadioModule } from '@angular/material/radio';
import { MatSelectModule } from '@angular/material/select';
import { Cart } from '../cart';

/**
 * A form that names its controls every way a real form does: a proper label, a placeholder with no
 * label, an aria-label, and one field identified only by a test id. Whether the framework finds all
 * four is the whole question.
 *
 * Material form fields wrap each control in several layers of generated markup, which is exactly why
 * this page is worth Materialising: the four naming channels are deliberately kept apart, so a run that
 * still fills all four is not relying on the shape of the DOM to do it.
 *
 * The shipping control stays a native `<select>` and the country is Material's own - which is a
 * `role="combobox"` that opens its options in an overlay somewhere else in the document. Both kinds on
 * one form, deliberately: `Select` drives only the first, `Choose` must drive both, and this page is
 * where that difference is provable.
 */
@Component({
  selector: 'app-checkout',
  imports: [
    FormsModule,
    MatButtonModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatInputModule,
    MatRadioModule,
    MatSelectModule,
  ],
  template: `
    <h2>Checkout</h2>

    <form (ngSubmit)="placeOrder()">
      <mat-form-field appearance="outline">
        <mat-label>Email</mat-label>
        <input matInput id="email" name="email" type="email" [(ngModel)]="email" />
      </mat-form-field>

      <!-- No label at all: a placeholder is the only thing naming this one. -->
      <mat-form-field appearance="outline">
        <input matInput name="street" type="text" placeholder="Street" [(ngModel)]="street" />
      </mat-form-field>

      <!-- Named for assistive technology rather than visibly. -->
      <mat-form-field appearance="outline">
        <input matInput name="city" type="text" aria-label="City" [(ngModel)]="city" />
      </mat-form-field>

      <!-- Nothing user-facing identifies this one, which is what test ids are for. -->
      <mat-form-field appearance="outline">
        <input matInput name="voucher" type="text" data-testid="voucher-code" [(ngModel)]="voucher" />
      </mat-form-field>

      <p class="row">
        <label for="shipping">Shipping</label>
        <select id="shipping" name="shipping" [(ngModel)]="shipping">
          <option value="standard">Standard</option>
          <option value="express">Express</option>
        </select>
      </p>

      <mat-form-field appearance="outline">
        <mat-label>Country</mat-label>
        <mat-select name="country" [(ngModel)]="country">
          <mat-option value="DE">Germany</mat-option>
          <mat-option value="AT">Austria</mat-option>
          <mat-option value="CH">Switzerland</mat-option>
        </mat-select>
      </mat-form-field>

      <fieldset>
        <legend>Payment</legend>
        <mat-radio-group name="payment" [(ngModel)]="payment">
          <mat-radio-button value="card">Card</mat-radio-button>
          <mat-radio-button value="invoice">Invoice</mat-radio-button>
        </mat-radio-group>
      </fieldset>

      <mat-checkbox name="terms" [(ngModel)]="terms">Accept terms</mat-checkbox>

      <div class="actions">
        <button mat-flat-button type="submit" [disabled]="!terms()">Place order</button>
      </div>
    </form>

    @if (error()) {
      <p role="alert" class="error">{{ error() }}</p>
    }
  `,
  styles: `
    form {
      max-width: 32rem;
      display: flex;
      flex-direction: column;
      gap: .35rem;
      padding: 1.35rem 1.5rem;
      background: var(--mat-sys-surface-container-low);
      border: 1px solid var(--mat-sys-outline-variant);
      border-radius: var(--mat-sys-corner-medium);
    }

    mat-form-field { width: 100%; }

    .row { display: flex; flex-wrap: wrap; align-items: center; gap: .6rem; margin: .25rem 0 .75rem; }
    .row label { min-width: 7rem; font: var(--mat-sys-body-medium); color: var(--mat-sys-on-surface-variant); }

    /* Styled to sit beside Material's fields without pretending to be one of them. */
    select {
      font: inherit;
      color: var(--mat-sys-on-surface);
      background: var(--mat-sys-surface);
      border: 1px solid var(--mat-sys-outline);
      border-radius: var(--mat-sys-corner-extra-small);
      padding: .5rem .6rem;
    }

    fieldset {
      margin: .25rem 0 1rem;
      padding: .6rem 1rem 1rem;
      border: 1px solid var(--mat-sys-outline-variant);
      border-radius: var(--mat-sys-corner-small);
    }

    legend { padding: 0 .35rem; font: var(--mat-sys-label-medium); color: var(--mat-sys-on-surface-variant); }
    mat-radio-group { display: flex; gap: 1rem; flex-wrap: wrap; }

    .actions { margin-top: 1.25rem; }

    .error {
      max-width: 32rem;
      margin-top: 1rem;
      padding: .6rem .8rem;
      color: var(--mat-sys-on-error-container);
      background: var(--mat-sys-error-container);
      border-radius: var(--mat-sys-corner-small);
      font-weight: 600;
    }
  `,
})
export class Checkout {
  private readonly cart = inject(Cart);
  private readonly router = inject(Router);

  protected readonly email = signal('');
  protected readonly street = signal('');
  protected readonly city = signal('');
  protected readonly voucher = signal('');
  protected readonly shipping = signal('standard');
  protected readonly country = signal('DE');
  protected readonly payment = signal('card');
  protected readonly terms = signal(false);
  protected readonly error = signal('');

  protected placeOrder(): void {
    if (!this.email().includes('@')) {
      this.error.set('Enter an email address.');

      return;
    }

    this.error.set('');
    void this.router.navigate(['/confirmation'], {
      queryParams: { total: this.cart.total(), shipping: this.shipping() },
    });
  }
}
