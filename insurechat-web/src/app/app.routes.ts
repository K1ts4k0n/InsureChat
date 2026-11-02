import { Routes } from '@angular/router';
import { adminGuard } from './core/admin-guard';

// chatbot/quote = public (ADR 0011). login เหลือไว้เป็นทางเข้า /admin เท่านั้น
export const routes: Routes = [
  { path: 'login', loadComponent: () => import('./features/login/login').then((m) => m.Login) },
  { path: 'quote', loadComponent: () => import('./features/quote/quote').then((m) => m.Quote) },
  { path: 'chat', loadComponent: () => import('./features/chatbot/chatbot').then((m) => m.Chatbot) },
  {
    path: 'admin',
    canActivate: [adminGuard],
    loadComponent: () => import('./features/admin/admin').then((m) => m.Admin)
  },
  { path: '', pathMatch: 'full', redirectTo: 'quote' },
  { path: '**', redirectTo: 'quote' }
];
