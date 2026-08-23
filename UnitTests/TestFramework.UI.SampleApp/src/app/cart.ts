import { Injectable, computed, signal } from '@angular/core';

export interface CartLine {
  readonly product: string;
  readonly quantity: number;
  readonly price: number;
}

/**
 * The little bit of state the pages share, so pressing a button on one page is observable on another.
 */
@Injectable({ providedIn: 'root' })
export class Cart {
  private readonly lines = signal<readonly CartLine[]>([]);

  readonly items = this.lines.asReadonly();
  readonly count = computed(() => this.lines().reduce((total, line) => total + line.quantity, 0));
  readonly total = computed(() => this.lines().reduce((sum, line) => sum + line.quantity * line.price, 0));

  add(product: string, price: number): void {
    this.lines.update(current => {
      const existing = current.find(line => line.product === product);

      return existing
        ? current.map(line => (line.product === product ? { ...line, quantity: line.quantity + 1 } : line))
        : [...current, { product, quantity: 1, price }];
    });
  }

  clear(): void {
    this.lines.set([]);
  }
}
