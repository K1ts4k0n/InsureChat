import {
  ApplicationConfig,
  provideBrowserGlobalErrorListeners,
  provideAppInitializer,
  inject
} from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';

import { routes } from './app.routes';
import { authInterceptor } from './core/auth-interceptor';
import { ThemeService } from './core/theme';
import { BrandTheme } from './core/brand-theme';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    provideHttpClient(withInterceptors([authInterceptor])),
    // set surface theme (light/dark) บน <html> ก่อนหน้าแรกวาด (กัน flash)
    provideAppInitializer(() => inject(ThemeService).init()),
    // ดึงสีแบรนด์ global จาก server แล้ว set --brand (ADR 0010/0011)
    provideAppInitializer(() => inject(BrandTheme).init())
  ]
};
