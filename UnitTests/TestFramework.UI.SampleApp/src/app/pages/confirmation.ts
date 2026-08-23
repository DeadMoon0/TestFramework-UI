import { Component, inject } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';

/**
 * Where a completed flow lands: a heading to expect, and a generated value to read back and carry
 * into the rest of the timeline.
 */
@Component({
  selector: 'app-confirmation',
  template: `
    <h2>Thank you</h2>

    <p>Your order is on its way.</p>

    <dl>
      <dt>Order number</dt>
      <dd data-testid="order-number">{{ orderNumber }}</dd>

      <dt>Shipping</dt>
      <dd data-testid="shipping">{{ shipping() }}</dd>
    </dl>
  `,
})
export class Confirmation {
  private readonly route = inject(ActivatedRoute);
  private readonly params = toSignal(this.route.queryParamMap, { requireSync: true });

  // Deliberately not derivable by the test: a value it can only learn by reading the page.
  protected readonly orderNumber = `A-${1000 + Math.floor(Math.random() * 9000)}`;

  protected readonly shipping = () => this.params().get('shipping') ?? 'standard';
}
