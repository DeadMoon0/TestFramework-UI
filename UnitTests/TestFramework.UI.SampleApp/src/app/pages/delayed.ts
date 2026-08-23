import { MatButtonModule } from '@angular/material/button';
import { MatProgressBarModule } from '@angular/material/progress-bar';
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
  imports: [MatButtonModule, MatProgressBarModule],
  template: `
    <h2>Orders</h2>

    <!-- A component reporting its state on attributes rather than in visible words: the element is
         always there, only data-state flips - the channel the attribute wait events watch. The
         heartbeat ticks forever, so "it changed from whatever it was" always has a change to see. -->
    <p
      data-testid="sync-state"
      [attr.data-state]="loaded() ? 'ready' : 'loading'"
      [attr.data-heartbeat]="heartbeat()"
    >
      {{ loaded() ? 'Orders are ready' : 'Fetching orders' }}
    </p>

    @if (!loaded()) {
      <mat-progress-bar mode="indeterminate" aria-label="Loading orders" />
      <p data-testid="spinner">Loading orders...</p>
    } @else {
      <p data-testid="loaded-note">3 orders loaded</p>

      @if (actionsReady()) {
        <button mat-flat-button type="button" (click)="exported.set(true)">Export all</button>
      }
    }

    @if (banner()) {
      <p role="status" data-testid="banner">Syncing with the warehouse</p>
    }

    @if (exported()) {
      <p data-testid="export-note">Export started</p>
    }
  `,
  styles: `
    mat-progress-bar { max-width: 20rem; margin-bottom: .6rem; }

    [role='status'] {
      display: inline-block;
      margin-top: 1rem;
      padding: .3rem .8rem;
      background: var(--mat-sys-tertiary-container);
      color: var(--mat-sys-on-tertiary-container);
      border-radius: var(--mat-sys-corner-full);
      font: var(--mat-sys-label-large);
    }
  `,
})
export class Delayed implements OnDestroy {
  protected readonly loaded = signal(false);
  protected readonly actionsReady = signal(false);
  protected readonly banner = signal(true);
  protected readonly exported = signal(false);
  protected readonly heartbeat = signal(0);

  private readonly timers: ReturnType<typeof setTimeout>[] = [
    setTimeout(() => this.loaded.set(true), 900),
    setTimeout(() => this.actionsReady.set(true), 1800),
    setTimeout(() => this.banner.set(false), 2400),
  ];

  private readonly pulse = setInterval(() => this.heartbeat.update(beats => beats + 1), 400);

  ngOnDestroy(): void {
    this.timers.forEach(timer => clearTimeout(timer));
    clearInterval(this.pulse);
  }
}
