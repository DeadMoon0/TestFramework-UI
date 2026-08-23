import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CurrencyPipe } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { Cart } from '../cart';

interface Product {
  readonly name: string;
  readonly price: number;
  readonly blurb: string;
}

/**
 * The everyday page: a list of things, each with a link and a button, and a counter that changes when
 * a button is pressed.
 *
 * Material cards and buttons here, which is the interesting part: the run finds "Add Anvil to cart" by
 * the accessible name the page gives it, and Material's own nesting - a button wrapping a ripple, a
 * touch target and a label span - changes nothing about that.
 */
@Component({
  selector: 'app-products',
  imports: [RouterLink, CurrencyPipe, MatButtonModule, MatCardModule],
  template: `
    <h2>Products</h2>

    <p data-testid="cart-summary">{{ cart.count() }} item{{ cart.count() === 1 ? '' : 's' }} in cart</p>

    <ul class="products">
      @for (product of products; track product.name) {
        <li>
          <mat-card appearance="outlined">
            <mat-card-content>
              <a [routerLink]="['/products']" [attr.aria-label]="product.name">{{ product.name }}</a>
              <span class="blurb">{{ product.blurb }}</span>
              <span class="price">{{ product.price | currency: 'EUR' }}</span>
              <!-- The visible text repeats on every row, so each button says which product it is for where
                   assistive technology (and a test) can read it. Exactly what a real accessible list does. -->
              <button
                mat-flat-button
                type="button"
                [attr.aria-label]="'Add ' + product.name + ' to cart'"
                (click)="cart.add(product.name, product.price)">Add to cart</button>
            </mat-card-content>
          </mat-card>
        </li>
      }
    </ul>

    <a mat-flat-button routerLink="/checkout" class="cta">Checkout</a>
  `,
  styles: `
    .products { list-style: none; padding: 0; margin: 0 0 1.5rem; display: flex; flex-direction: column; gap: .75rem; }

    mat-card-content {
      display: flex;
      flex-wrap: wrap;
      gap: .6rem 1rem;
      align-items: center;
      padding-block: .35rem;
    }

    mat-card-content > a { font: var(--mat-sys-title-small); color: var(--mat-sys-on-surface); text-decoration: none; }
    mat-card-content > a:hover { color: var(--mat-sys-primary); text-decoration: underline; }

    .blurb { color: var(--mat-sys-on-surface-variant); flex: 1 1 12rem; font: var(--mat-sys-body-small); }
    .price { font: var(--mat-sys-title-small); font-variant-numeric: tabular-nums; }

    [data-testid='cart-summary'] {
      display: inline-block;
      margin-bottom: 1.5rem;
      padding: .25rem .8rem;
      background: var(--mat-sys-secondary-container);
      color: var(--mat-sys-on-secondary-container);
      border-radius: var(--mat-sys-corner-full);
      font: var(--mat-sys-label-large);
    }
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
