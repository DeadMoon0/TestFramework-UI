import { Component, signal } from '@angular/core';

/**
 * A page that is simply broken: it logs an error on load, and pressing the button throws.
 *
 * The framework must not report this as "the button was not found" or "the text never appeared". A
 * crashed application and a mis-named locator are different problems with different fixes, and the
 * failure message has to tell them apart.
 */
@Component({
  selector: 'app-broken',
  template: `
    <h2>Reports</h2>

    <button type="button" (click)="generate()">Generate report</button>

    @if (done()) {
      <p data-testid="report-note">Report ready</p>
    }
  `,
})
export class Broken {
  protected readonly done = signal(false);

  constructor() {
    console.error('ReportService: configuration endpoint returned 500');
  }

  protected generate(): void {
    // Genuinely throws, the way a real bug would, so nothing downstream ever appears.
    const config = undefined as unknown as { template: string };

    this.done.set(config.template.length > 0);
  }
}
