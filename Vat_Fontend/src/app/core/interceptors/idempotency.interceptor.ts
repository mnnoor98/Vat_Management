import { HttpInterceptorFn, HttpRequest, HttpHandlerFn, HttpEvent } from '@angular/common/http';
import { Observable } from 'rxjs';

/**
 * IdempotencyInterceptor
 * Automatically attaches an RFC 4122 v4 UUID Idempotency-Key header to all mutating HTTP requests (POST, PUT, DELETE, PATCH).
 * This satisfies the ASP.NET Core 10 backend requirement in CommandExecutor.cs and prevents duplicate transactions.
 */
function generateUuid(): string {
  if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') {
    try {
      return crypto.randomUUID();
    } catch {
      // Fallback if randomUUID threw
    }
  }
  // Standard RFC4122 version 4 UUID compliant with ASP.NET Core Guid.ParseExact(..., "D")
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (c) => {
    const r = (Math.random() * 16) | 0;
    const v = c === 'x' ? r : (r & 0x3) | 0x8;
    return v.toString(16);
  });
}

export const idempotencyInterceptor: HttpInterceptorFn = (
  req: HttpRequest<unknown>,
  next: HttpHandlerFn
): Observable<HttpEvent<unknown>> => {
  const mutatingMethods = ['POST', 'PUT', 'DELETE', 'PATCH'];

  if (mutatingMethods.includes(req.method.toUpperCase())) {
    const correlationId = generateUuid();
    const idempotencyKey = req.headers.get('Idempotency-Key') || generateUuid();

    const cloned = req.clone({
      setHeaders: {
        'Idempotency-Key': idempotencyKey,
        'X-Correlation-ID': correlationId
      }
    });

    return next(cloned);
  }

  return next(req);
};

