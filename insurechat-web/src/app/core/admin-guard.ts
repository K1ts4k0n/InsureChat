import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { Auth } from './auth';

/** กันหน้า /admin — ต้อง login และ role = admin (ADR 0011) */
export const adminGuard: CanActivateFn = () => {
  const auth = inject(Auth);
  const router = inject(Router);
  if (auth.isLoggedIn() && auth.role() === 'admin') return true;
  router.navigate(['/login']);
  return false;
};
