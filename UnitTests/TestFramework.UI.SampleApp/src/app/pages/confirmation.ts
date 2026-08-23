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
  styles: `
    dl {
      display: grid;
      grid-template-columns: auto 1fr;
      gap: .5rem 1.5rem;
      max-width: 26rem;
      margin: 0;
      padding: 1.15rem 1.35rem;
      background: var(--mat-sys-surface-container-low);
      border: 1px solid var(--mat-sys-outline-variant);
      border-radius: var(--mat-sys-corner-medium);
    }

    dt { font: var(--mat-sys-label-large); color: var(--mat-sys-on-surface-variant); }
    dd { margin: 0; font: var(--mat-sys-title-small); font-variant-numeric: tabular-nums; }
  `,
})
export class Confirmation {
  private readonly route = inject(ActivatedRoute);
  private readonly params = toSignal(this.route.queryParamMap, { requireSync: true });

  // Deliberately not derivable by the test: a value it can only learn by reading the page.
  protected readonly orderNumber = `A-${1000 + Math.floor(Math.random() * 9000)}`;

  protected readonly shipping = () => this.params().get('shipping') ?? 'standard';
}
