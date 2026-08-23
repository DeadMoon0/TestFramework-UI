import { Routes } from '@angular/router';

/**
 * Every page here is a fixture for one thing the framework claims.
 *
 * The "simple" half is what any test does - press a control, fill a form, read a value. The
 * "resilience" half is the same page after somebody changed it: a control renamed, one moved into a
 * different layout, three that answer to the same name. The "complex" half is the realistic case,
 * built from components with their own element names, because that is the markup a real application
 * hands a test and nobody tuned it for one.
 */
export const routes: Routes = [
  { path: '', redirectTo: 'products', pathMatch: 'full' },

  // Simple: the everyday cases.
  { path: 'products', loadComponent: () => import('./pages/products').then(m => m.Products) },
  { path: 'checkout', loadComponent: () => import('./pages/checkout').then(m => m.Checkout) },
  { path: 'confirmation', loadComponent: () => import('./pages/confirmation').then(m => m.Confirmation) },

  // Resilience: the same intent after the page changed.
  { path: 'renamed', loadComponent: () => import('./pages/renamed').then(m => m.Renamed) },
  { path: 'moved', loadComponent: () => import('./pages/moved').then(m => m.Moved) },
  { path: 'ambiguous', loadComponent: () => import('./pages/ambiguous').then(m => m.Ambiguous) },
  { path: 'delayed', loadComponent: () => import('./pages/delayed').then(m => m.Delayed) },
  { path: 'scrolling', loadComponent: () => import('./pages/scrolling').then(m => m.Scrolling) },
  { path: 'interactions', loadComponent: () => import('./pages/interactions').then(m => m.Interactions) },
  { path: 'broken', loadComponent: () => import('./pages/broken').then(m => m.Broken) },
  { path: 'responsive', loadComponent: () => import('./pages/responsive').then(m => m.Responsive) },

  // Complex: components, custom element names, live data, a dialog, tabs.
  { path: 'orders', loadComponent: () => import('./pages/orders').then(m => m.Orders) },

  { path: '**', redirectTo: 'products' },
];
