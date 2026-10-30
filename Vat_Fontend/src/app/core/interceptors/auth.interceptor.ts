import { inject } from '@angular/core';
import { HttpInterceptorFn } from '@angular/common/http';
import { AuthService } from '../services/auth.service';
import { environment } from '../../../environments/environment';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const target = new URL(req.url, location.origin);
  const api = new URL(environment.apiGatewayUrl, location.origin);
  const isApi = target.origin === api.origin && target.pathname.startsWith(`${api.pathname.replace(/\/$/, '')}/`);
  const token = isApi ? auth.accessToken() : null;
  return next(token && !req.headers.has('Authorization')
    ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : req);
};
