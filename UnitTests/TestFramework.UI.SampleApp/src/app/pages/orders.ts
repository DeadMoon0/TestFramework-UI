import { Component, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { MatButtonModule } from '@angular/material/button';
import { MatTabsModule } from '@angular/material/tabs';
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
  imports: [OrderList, MatButtonModule, MatTabsModule],
  template: `
    <h2>Orders</h2>

    <!-- Material's tab group renders role="tablist" and role="tab" of its own accord, so the semantics a
         test reasons about survive the swap; only the markup underneath them changed. The list stays
         outside the group because the tabs filter it rather than containing it. -->
    <mat-tab-group
      aria-label="Status"
      [selectedIndex]="tabIndex()"
      (selectedIndexChange)="selectTab($event)">
      @for (tab of tabs; track tab) {
        <mat-tab [label]="tab" />
      }
    </mat-tab-group>

    @if (loading()) {
      <p data-testid="orders-loading">Loading orders...</p>
    } @else {
      <app-order-list [orders]="visible()" [label]="'Orders ' + activeTab()" (cancel)="askToCancel($event)" />

      @if (!showAll() && filtered().length > pageSize) {
        <button mat-stroked-button type="button" (click)="showAll.set(true)">Load more</button>
      }
    }

    @if (pendingCancel()) {
      <div role="dialog" aria-label="Cancel order" class="dialog">
        <p>Cancel order {{ pendingCancel() }}?</p>
        <button mat-flat-button type="button" (click)="confirmCancel()">Yes, cancel it</button>
        <button mat-button type="button" (click)="pendingCancel.set('')">Keep it</button>
      </div>
    }

    @if (cancelled()) {
      <p role="status" data-testid="cancelled-note">Order {{ cancelled() }} cancelled</p>
    }
  `,
  styles: `
    mat-tab-group { margin-bottom: 1.5rem; }

    /* The tab group carries no panels, so its body would otherwise take up room for nothing. */
    ::ng-deep .mat-mdc-tab-body-wrapper { display: none; }

    .dialog {
      margin-top: 1.5rem;
      max-width: 24rem;
      padding: 1.15rem 1.35rem;
      background: var(--mat-sys-surface-container-high);
      border-radius: var(--mat-sys-corner-large);
      box-shadow: var(--mat-sys-level3);
    }

    .dialog p { font: var(--mat-sys-title-small); }
    .dialog button + button { margin-left: .5rem; }
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

  protected tabIndex(): number {
    return this.tabs.indexOf(this.activeTab() as (typeof this.tabs)[number]);
  }

  protected selectTab(index: number): void {
    this.activeTab.set(this.tabs[index]);
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
