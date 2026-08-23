import { Component, computed, input, output } from '@angular/core';
import { CurrencyPipe } from '@angular/common';
import { Order } from './order';
import { OrderRow } from './order-row';

/**
 * The list itself: a custom element containing a header row and one `<app-order-row>` per order, with
 * its own count as an attribute.
 *
 * This is the shape a structure expectation is written against - a named container, a cardinality of
 * rows, an attribute worth a rule - and none of it is a CSS selector.
 */
@Component({
  selector: 'app-order-list',
  imports: [OrderRow, CurrencyPipe],
  host: {
    role: 'table',
    '[attr.data-count]': 'orders().length',
    '[attr.aria-label]': 'label()',
  },
  template: `
    <div class="head" role="row">
      <span role="columnheader">Order</span>
      <span role="columnheader">Product</span>
      <span role="columnheader">Qty</span>
      <span role="columnheader">Price</span>
      <span role="columnheader">Status</span>
      <span role="columnheader">Actions</span>
    </div>

    @for (order of orders(); track order.id) {
      <app-order-row [order]="order" (cancel)="cancel.emit($event)" />
    }

    @if (orders().length === 0) {
      <p class="empty" data-testid="orders-empty">No orders yet</p>
    }

    <p class="total" data-testid="orders-total">{{ total() | currency: 'EUR' }}</p>
  `,
  styles: `
    /* One column definition, used by the header here and by every row component. Changing it is a
       layout change and nothing else - no test knows these widths exist. */
    :host {
      --order-columns: 6.5rem 1fr 3.5rem 7rem 7.5rem 6rem;

      display: block;
      background: var(--mat-sys-surface-container-lowest);
      border: 1px solid var(--mat-sys-outline-variant);
      border-radius: var(--mat-sys-corner-medium);
      overflow: hidden;
    }

    .head {
      display: grid;
      grid-template-columns: var(--order-columns);
      gap: .5rem;
      padding: .7rem 1rem;
      background: var(--mat-sys-surface-container);
      border-bottom: 1px solid var(--mat-sys-outline-variant);
      font: var(--mat-sys-label-medium);
      color: var(--mat-sys-on-surface-variant);
    }

    .empty { margin: 0; padding: 1.75rem 1rem; text-align: center; color: var(--mat-sys-on-surface-variant); }

    .total {
      margin: 0;
      padding: .8rem 1rem;
      text-align: right;
      font: var(--mat-sys-title-small);
      font-variant-numeric: tabular-nums;
      border-top: 1px solid var(--mat-sys-outline-variant);
      background: var(--mat-sys-surface-container);
    }
  `,
})
export class OrderList {
  readonly orders = input.required<readonly Order[]>();
  readonly label = input('Orders');
  readonly cancel = output<string>();

  protected readonly total = computed(() =>
    this.orders().reduce((sum, order) => sum + order.quantity * order.price, 0));
}
