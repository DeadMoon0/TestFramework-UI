import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CurrencyPipe } from '@angular/common';
import { Cart } from '../cart';

interface Product {
  readonly name: string;
  readonly price: number;
  readonly blurb: string;
}

/**
 * The everyday page: a list of things, each with a link and a button, and a counter that changes when
 * a button is pressed.
 */
@Component({
  selector: 'app-products',
  imports: [RouterLink, CurrencyPipe],
  template: `
    <h2>Products</h2>

    <p data-testid="cart-summary">{{ cart.count() }} item{{ cart.count() === 1 ? '' : 's' }} in cart</p>

    <ul class="products">
      @for (product of products; track product.name) {
        <li>
          <a [routerLink]="['/products']" [attr.aria-label]="product.name">{{ product.name }}</a>
          <span class="blurb">{{ product.blurb }}</span>
          <span class="price">{{ product.price | currency: 'EUR' }}</span>
          <!-- The visible text repeats on every row, so each button says which product it is for where
               assistive technology (and a test) can read it. Exactly what a real accessible list does. -->
          <button
            type="button"
            [attr.aria-label]="'Add ' + product.name + ' to cart'"
            (click)="cart.add(product.name, product.price)">Add to cart</button>
        </li>
      }
    </ul>

    <a routerLink="/checkout" class="cta">Checkout</a>
  `,
  styles: `
    .products { list-style: none; padding: 0; }
    .products li { display: flex; gap: 1rem; align-items: center; padding: .5rem 0; border-bottom: 1px solid #ddd; }
    .blurb { color: #666; flex: 1; }
    .cta { display: inline-block; margin-top: 1rem; font-weight: 600; }
  `,
})
export class Products {
  protected readonly cart = inject(Cart);

  protected readonly products: readonly Product[] = [
    { name: 'Anvil', price: 129, blurb: 'Heavy. Reliable. Loud.' },
    { name: 'Rope', price: 9, blurb: 'Twelve metres of it.' },
    { name: 'Crate', price: 24.5, blurb: 'Holds an anvil, barely.' },
  ];
}
