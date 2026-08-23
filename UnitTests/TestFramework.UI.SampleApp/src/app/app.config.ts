import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withFetch } from '@angular/common/http';
import { routes } from './app.routes';

// Real path-based routing rather than hash URLs: the address bar is something tests assert on, and it
// should look like the applications this framework will actually be pointed at. The host serving the
// build output falls back to index.html for unresolved paths under the app's prefix.
export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideHttpClient(withFetch()),
    provideRouter(routes),
  ],
};
