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
  imports: [CurrencyPipe],
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
      <button type="button" [attr.aria-label]="'Cancel ' + order().id" (click)="cancel.emit(order().id)">
        Cancel
      </button>
    </span>
  `,
  styles: `
    :host { display: grid; grid-template-columns: 6rem 1fr 3rem 6rem 7rem 6rem; align-items: center; padding: .35rem 0; border-bottom: 1px solid #eee; }
    .chip { border-radius: 999px; padding: .1rem .6rem; background: #eef; font-size: .85em; }
    :host([data-status='shipped']) .chip { background: #e6f4ea; }
  `,
})
export class OrderRow {
  readonly order = input.required<Order>();
  readonly cancel = output<string>();
}
