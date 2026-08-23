import { Component, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Order } from '../orders/order';
import { OrderList } from '../orders/order-list';

/**
 * The realistic page: data loaded over HTTP, rendered through components, filtered by tabs, extended
 * by a "Load more", and a confirmation dialog in front of a destructive action.
 *
 * Everything a test does here goes through markup nobody arranged for the test's convenience, which is
 * the only honest way to find out whether the framework's claims hold.
 */
@Component({
  selector: 'app-orders',
  imports: [OrderList],
  template: `
    <h2>Orders</h2>

    <div role="tablist" aria-label="Status">
      @for (tab of tabs; track tab) {
        <button
          type="button"
          role="tab"
          [attr.aria-selected]="activeTab() === tab"
          (click)="activeTab.set(tab)">{{ tab }}</button>
      }
    </div>

    @if (loading()) {
      <p data-testid="orders-loading">Loading orders...</p>
    } @else {
      <app-order-list [orders]="visible()" [label]="'Orders ' + activeTab()" (cancel)="askToCancel($event)" />

      @if (!showAll() && filtered().length > pageSize) {
        <button type="button" (click)="showAll.set(true)">Load more</button>
      }
    }

    @if (pendingCancel()) {
      <div role="dialog" aria-label="Cancel order" class="dialog">
        <p>Cancel order {{ pendingCancel() }}?</p>
        <button type="button" (click)="confirmCancel()">Yes, cancel it</button>
        <button type="button" (click)="pendingCancel.set('')">Keep it</button>
      </div>
    }

    @if (cancelled()) {
      <p role="status" data-testid="cancelled-note">Order {{ cancelled() }} cancelled</p>
    }
  `,
  styles: `
    [role='tablist'] { display: flex; gap: .5rem; margin-bottom: 1rem; }
    [role='tab'][aria-selected='true'] { font-weight: 700; text-decoration: underline; }
    .dialog { border: 2px solid #333; padding: 1rem; margin-top: 1rem; max-width: 22rem; }
  `,
})
export class Orders {
  private readonly http = inject(HttpClient);

  protected readonly pageSize = 2;
  protected readonly tabs = ['All', 'Packing', 'Shipped'] as const;

  protected readonly orders = signal<readonly Order[]>([]);
  protected readonly loading = signal(true);
  protected readonly activeTab = signal<string>('All');
  protected readonly showAll = signal(false);
  protected readonly pendingCancel = signal('');
  protected readonly cancelled = signal('');

  protected readonly filtered = computed(() => {
    const tab = this.activeTab();

    return tab === 'All'
      ? this.orders()
      : this.orders().filter(order => order.status === tab);
  });

  protected readonly visible = computed(() =>
    this.showAll() ? this.filtered() : this.filtered().slice(0, this.pageSize));

  constructor() {
    // A real request to the host, so the page has a genuine loading state rather than a simulated one.
    this.http.get<readonly Order[]>('/api/orders').subscribe({
      next: orders => {
        this.orders.set(orders);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  protected askToCancel(id: string): void {
    this.pendingCancel.set(id);
  }

  protected confirmCancel(): void {
    const id = this.pendingCancel();

    this.orders.update(current => current.filter(order => order.id !== id));
    this.pendingCancel.set('');
    this.cancelled.set(id);
  }
}
