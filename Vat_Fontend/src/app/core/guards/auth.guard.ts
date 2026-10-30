import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

/**
 * AuthGuard
 * Protects routes requiring authentication. If the user is not authenticated,
 * redirects them to the login page with a returnUrl parameter.
 */
export const authGuard: CanActivateFn = async (route, state) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  await authService.ready;
  if (authService.hasValidSession()) {
    return true;
  }

  // Not authenticated, redirect to login page with returnUrl
  return router.createUrlTree(['/login'], {
    queryParams: { returnUrl: state.url }
  });
};

/**
 * GuestGuard
 * Protects login/register routes. If the user is already authenticated,
 * redirects them to the dashboard.
 */
export const guestGuard: CanActivateFn = async () => {
  const authService = inject(AuthService);
  const router = inject(Router);

  await authService.ready;
  if (authService.hasValidSession()) {
    return router.createUrlTree(['/app/dashboard']);
  }

  return true;
};
