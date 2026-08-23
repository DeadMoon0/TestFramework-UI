import { Component, OnDestroy, signal } from '@angular/core';

/**
 * Content that is simply not there yet: a slow load, then a control that appears only after another
 * delay, then a banner that disappears again.
 *
 * Waiting is the framework's job, so a test here must contain no sleeps - and the disappearing banner
 * is what proves a negative expectation waits rather than checking once and passing by luck.
 */
@Component({
  selector: 'app-delayed',
  template: `
    <h2>Orders</h2>

    @if (!loaded()) {
      <p data-testid="spinner">Loading orders...</p>
    } @else {
      <p data-testid="loaded-note">3 orders loaded</p>

      @if (actionsReady()) {
        <button type="button" (click)="exported.set(true)">Export all</button>
      }
    }

    @if (banner()) {
      <p role="status" data-testid="banner">Syncing with the warehouse</p>
    }

    @if (exported()) {
      <p data-testid="export-note">Export started</p>
    }
  `,
})
export class Delayed implements OnDestroy {
  protected readonly loaded = signal(false);
  protected readonly actionsReady = signal(false);
  protected readonly banner = signal(true);
  protected readonly exported = signal(false);

  private readonly timers: ReturnType<typeof setTimeout>[] = [
    setTimeout(() => this.loaded.set(true), 900),
    setTimeout(() => this.actionsReady.set(true), 1800),
    setTimeout(() => this.banner.set(false), 2400),
  ];

  ngOnDestroy(): void {
    this.timers.forEach(timer => clearTimeout(timer));
  }
}
