import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { Cart } from '../cart';

/**
 * A form that names its controls every way a real form does: a proper label, a placeholder with no
 * label, an aria-label, and one field identified only by a test id. Whether the framework finds all
 * four is the whole question.
 */
@Component({
  selector: 'app-checkout',
  imports: [FormsModule],
  template: `
    <h2>Checkout</h2>

    <form (ngSubmit)="placeOrder()">
      <p>
        <label for="email">Email</label>
        <input id="email" name="email" type="email" [(ngModel)]="email" />
      </p>

      <p>
        <!-- No label at all: a placeholder is the only thing naming this one. -->
        <input name="street" type="text" placeholder="Street" [(ngModel)]="street" />
      </p>

      <p>
        <!-- Named for assistive technology rather than visibly. -->
        <input name="city" type="text" aria-label="City" [(ngModel)]="city" />
      </p>

      <p>
        <!-- Nothing user-facing identifies this one, which is what test ids are for. -->
        <input name="voucher" type="text" data-testid="voucher-code" [(ngModel)]="voucher" />
      </p>

      <p>
        <label for="shipping">Shipping</label>
        <select id="shipping" name="shipping" [(ngModel)]="shipping">
          <option value="standard">Standard</option>
          <option value="express">Express</option>
        </select>
      </p>

      <fieldset>
        <legend>Payment</legend>
        <label><input type="radio" name="payment" value="card" [(ngModel)]="payment" /> Card</label>
        <label><input type="radio" name="payment" value="invoice" [(ngModel)]="payment" /> Invoice</label>
      </fieldset>

      <p>
        <label><input type="checkbox" name="terms" [(ngModel)]="terms" /> Accept terms</label>
      </p>

      <button type="submit" [disabled]="!terms()">Place order</button>
    </form>

    @if (error()) {
      <p role="alert" class="error">{{ error() }}</p>
    }
  `,
  styles: `
    form p { margin: .6rem 0; }
    label { display: inline-block; min-width: 7rem; }
    .error { color: #b00020; font-weight: 600; }
    fieldset { margin: .8rem 0; }
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
