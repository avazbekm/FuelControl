import { ApplicationConfig, inject, isDevMode, provideAppInitializer, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter, withHashLocation, withComponentInputBinding } from '@angular/router';
import { provideServiceWorker } from '@angular/service-worker';
import { routes } from './app.routes';
import { Sozlama } from './core/sozlama';
import { apiManzilOrnat } from './api/api';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    // sozlama.json (API manzili) — birinchi so'rovdan oldin o'qiladi
    provideAppInitializer(async () => {
      const s = inject(Sozlama);
      await s.yukla();
      apiManzilOrnat(s.api);
    }),
    provideRouter(routes, withHashLocation(), withComponentInputBinding()),
    provideServiceWorker('ngsw-worker.js', {
      enabled: !isDevMode(),
      registrationStrategy: 'registerImmediately',
    }),
  ],
};
