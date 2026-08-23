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
    :host { display: block; border-top: 2px solid #333; }
    .head { display: grid; grid-template-columns: 6rem 1fr 3rem 6rem 7rem 6rem; font-weight: 600; padding: .35rem 0; }
    .total { text-align: right; font-weight: 600; }
  `,
})
export class OrderList {
  readonly orders = input.required<readonly Order[]>();
  readonly label = input('Orders');
  readonly cancel = output<string>();

  protected readonly total = computed(() =>
    this.orders().reduce((sum, order) => sum + order.quantity * order.price, 0));
}
