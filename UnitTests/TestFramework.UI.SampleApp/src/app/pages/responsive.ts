import { MatButtonModule } from '@angular/material/button';
import { Component, signal } from '@angular/core';

/**
 * The same page as a phone and as a desktop sees it: below 768px the navigation collapses behind a
 * burger button, above it the links are simply there.
 *
 * One timeline should cover both by pointing at a different configured device - no second test, no
 * branch inside the test. Which means the phone run has to press "Menu" first, and the desktop run
 * must not find it at all.
 */
@Component({
  selector: 'app-responsive',
  imports: [MatButtonModule],
  template: `
    <h2>Catalogue</h2>

    <button mat-stroked-button type="button" class="burger" (click)="open.set(!open())" aria-label="Menu">☰ Menu</button>

    <nav [class.open]="open()" aria-label="Catalogue">
      <a href="#tools">Tools</a>
      <a href="#materials">Materials</a>
      <a href="#offers">Offers</a>
    </nav>

    <p data-testid="viewport-note">{{ open() ? 'Menu open' : 'Menu closed' }}</p>
  `,
  styles: `
    /* Desktop: the links are present and the burger is display:none, which takes it out of the
       accessibility tree - so a run at 1080p cannot perceive it any more than a person could.

       These four declarations are this page's behaviour and its entire reason for existing. Everything
       below them is only how it looks. */
    .burger { display: none; }
    nav { display: flex; gap: .35rem; }

    @media (max-width: 767px) {
      .burger { display: inline-block; }
      nav { display: none; }
      nav.open { display: flex; flex-direction: column; }
    }

    nav {
      width: fit-content;
      padding: .3rem;
      background: var(--mat-sys-surface-container);
      border-radius: var(--mat-sys-corner-medium);
    }

    nav a {
      padding: .4rem .85rem;
      border-radius: var(--mat-sys-corner-small);
      color: var(--mat-sys-on-surface-variant);
      text-decoration: none;
      font: var(--mat-sys-label-large);
    }

    nav a:hover { background: var(--mat-sys-surface-container-highest); color: var(--mat-sys-primary); }

    [data-testid='viewport-note'] { margin-top: 1rem; color: var(--mat-sys-on-surface-variant); font: var(--mat-sys-body-small); }
  `,
})
export class Responsive {
  protected readonly open = signal(false);
}
