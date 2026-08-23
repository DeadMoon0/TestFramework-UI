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
  private static readonly storageKey = 'sample-app.cart';

  private readonly lines = signal<readonly CartLine[]>(Cart.restore());

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

    this.persist();
  }

  clear(): void {
    this.lines.set([]);
    this.persist();
  }

  /**
   * The cart survives a reload, the way a real one does.
   *
   * It also gives a test something to check isolation with: if one run's cart were visible to the next,
   * this is where it would show.
   */
  private persist(): void {
    localStorage.setItem(Cart.storageKey, JSON.stringify(this.lines()));
  }

  private static restore(): readonly CartLine[] {
    try {
      const stored = localStorage.getItem(Cart.storageKey);

      return stored ? (JSON.parse(stored) as CartLine[]) : [];
    } catch {
      return [];
    }
  }
}
