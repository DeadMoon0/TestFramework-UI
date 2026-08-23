import { MatButtonModule } from '@angular/material/button';
import { Component, input, output } from '@angular/core';
import { CurrencyPipe } from '@angular/common';
import { Order } from './order';

/**
 * One row of the order list.
 *
 * The component's own element name ends up in the document as `<app-order-row>`, which is exactly the
 * markup a structure expectation should be able to name. It also carries its state as attributes
 * rather than only as classes, because an attribute is something a test can reason about and a
 * generated class name is not.
 */
@Component({
  selector: 'app-order-row',
  imports: [CurrencyPipe, MatButtonModule],
  host: {
    role: 'row',
    '[attr.data-status]': 'order().status.toLowerCase()',
    '[attr.data-order-id]': 'order().id',
  },
  template: `
    <span class="cell cell--id" role="cell">{{ order().id }}</span>
    <span class="cell cell--product" role="cell">{{ order().product }}</span>
    <span class="cell cell--qty" role="cell">{{ order().quantity }}</span>
    <span class="cell cell--price" role="cell">{{ order().price | currency: 'EUR' }}</span>
    <span class="cell cell--status" role="cell">
      <span class="chip">{{ order().status }}</span>
    </span>
    <span class="cell cell--actions" role="cell">
      <button
        mat-button
        type="button"
        [attr.aria-label]="'Cancel ' + order().id"
        (click)="cancel.emit(order().id)">Cancel</button>
    </span>
  `,
  styles: `
    :host {
      display: grid;
      grid-template-columns: var(--order-columns, 6.5rem 1fr 3.5rem 7rem 7.5rem 6rem);
      gap: .5rem;
      align-items: center;
      padding: .4rem 1rem;
      border-bottom: 1px solid var(--mat-sys-outline-variant);
    }

    :host(:last-of-type) { border-bottom: 0; }
    :host(:hover) { background: var(--mat-sys-surface-container-low); }

    .cell { min-width: 0; }
    .cell--id { font-family: ui-monospace, monospace; font-size: .85rem; color: var(--mat-sys-on-surface-variant); }
    .cell--product { font: var(--mat-sys-title-small); }
    .cell--qty, .cell--price { font-variant-numeric: tabular-nums; }
    .cell--actions { text-align: right; }

    /* The status colour comes off the same attribute a structure expectation reads, rather than off a
       class only the stylesheet understands. */
    :host([data-status='shipped']) .chip { background: var(--shop-shipped-surface); color: var(--shop-shipped); }
    :host([data-status='packing']) .chip { background: var(--shop-packing-surface); color: var(--shop-packing); }
  `,
})
export class OrderRow {
  readonly order = input.required<Order>();
  readonly cancel = output<string>();
}
