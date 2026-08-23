import { Component, signal } from '@angular/core';

/**
 * A page where the pointer and the keyboard are the behaviour: a card that exists only while hovered,
 * a row that renames on double-click, one that offers options on right-click, a drag that has to land,
 * and an input that counts its keystrokes - so a test can prove its keys were real keys, not a value
 * set in one motion.
 */
@Component({
  selector: 'app-interactions',
  template: `
    <h2>Interactions</h2>

    <section aria-label="Pointer">
      <h3>Pointer</h3>

      <button
        type="button"
        (mouseenter)="hoverCard.set(true)"
        (mouseleave)="hoverCard.set(false)"
      >
        Delivery details
      </button>
      @if (hoverCard()) {
        <p role="tooltip" data-testid="hover-card">Delivered from the Bergen warehouse in 2 to 4 days.</p>
      }

      <p class="row" (dblclick)="renaming.set(true)">Quarterly report.pdf</p>
      @if (renaming()) {
        <p data-testid="rename-note">Rename started</p>
      }

      <p class="row" (contextmenu)="menu.set(true); $event.preventDefault()">Archive entry</p>
      @if (menu()) {
        <p role="menu" data-testid="context-menu">Archive options open</p>
      }

      <p class="row" draggable="true" (dragstart)="armDrag($event)">Drag the anvil</p>
      <p
        class="row"
        data-testid="drop-zone"
        (dragover)="$event.preventDefault()"
        (drop)="dropped.set(true); $event.preventDefault()"
      >
        Drop zone for heavy goods
      </p>
      @if (dropped()) {
        <p data-testid="drop-note">The anvil landed</p>
      }
    </section>

    <section aria-label="Keyboard">
      <h3>Keyboard</h3>

      <label for="search">Search</label>
      <input id="search" type="text" (keydown)="countKey($event)" />
      <p data-testid="key-count">{{ keys() }} keys pressed</p>
      @if (shortcut()) {
        <p data-testid="shortcut-note">Search-everywhere opened</p>
      }
    </section>
  `,
  styles: `
    section { margin-bottom: 1.4rem; }

    .row {
      max-width: 26rem;
      padding: .5rem .9rem;
      margin: .3rem 0;
      background: var(--mat-sys-surface-container);
      border-radius: var(--mat-sys-corner-medium);
      font: var(--mat-sys-body-medium);
      user-select: none;
    }

    [role='tooltip'] {
      display: inline-block;
      padding: .3rem .8rem;
      background: var(--mat-sys-inverse-surface);
      color: var(--mat-sys-inverse-on-surface);
      border-radius: var(--mat-sys-corner-small);
      font: var(--mat-sys-body-small);
    }
  `,
})
export class Interactions {
  protected readonly hoverCard = signal(false);
  protected readonly renaming = signal(false);
  protected readonly menu = signal(false);
  protected readonly dropped = signal(false);
  protected readonly keys = signal(0);
  protected readonly shortcut = signal(false);

  protected armDrag(event: DragEvent): void {
    event.dataTransfer?.setData('text/plain', 'anvil');
  }

  protected countKey(event: KeyboardEvent): void {
    this.keys.update(count => count + 1);

    if (event.ctrlKey && event.key === 'Enter') {
      this.shortcut.set(true);
    }
  }
}
