import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { ApplicationConfig, inject, provideAppInitializer, provideZoneChangeDetection } from '@angular/core';
import { provideRouter } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { routes } from '../routes/app.routes';
import { apiBaseUrlInterceptor } from './api-base-url.interceptor';
import { authInterceptor } from '../shared/auth/auth.interceptor';
import { AuthService } from '../shared/auth/auth.service';

export const appConfig: ApplicationConfig = {
  providers: [
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(routes),
    provideHttpClient(withInterceptors([apiBaseUrlInterceptor, authInterceptor])),
    provideAppInitializer(() => firstValueFrom(inject(AuthService).restoreSession()))
  ]
};
