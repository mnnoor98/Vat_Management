import { HttpInterceptorFn, HttpRequest, HttpHandlerFn, HttpEvent, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { ToastService } from '../services/toast.service';
import { AuthService } from '../services/auth.service';
import { CompanyContextService } from '../services/company-context.service';

// Surface server failures; a rejected session requires issuer reauthentication. Commands are never silently replayed.
export const errorInterceptor: HttpInterceptorFn = (
  req: HttpRequest<unknown>,
  next: HttpHandlerFn
): Observable<HttpEvent<unknown>> => {
  const toast = inject(ToastService);
  const authService = inject(AuthService);
  const router = inject(Router);
  const context = inject(CompanyContextService);

  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      // 1. Handle 401 Unauthorized with Token Refresh
      if (error.status === 401) {
        return handle401Error(req, next, error, authService, router, toast);
      }

      // 2. Extract RFC 7807 ProblemDetails or standard error message
      let title = 'সার্ভার ত্রুটি (Request Failed)';
      let message = 'অনুরোধটি সম্পন্ন করা যায়নি। অনুগ্রহ করে পুনরায় চেষ্টা করুন।';
      let isWarning = false;

      if (error.error && typeof error.error === 'object') {
        const problem = error.error as {
          title?: string;
          detail?: string;
          message?: string;
          traceId?: string;
          errors?: Record<string, string[]>;
        };

        if (problem.title) title = problem.title;
        if (problem.detail) message = problem.detail;
        else if (problem.message) message = problem.message;
        else if (problem.errors) {
          const errorDetails: string[] = [];
          for (const key of Object.keys(problem.errors)) {
            const msgs = problem.errors[key];
            if (msgs && msgs.length > 0) {
              errorDetails.push(`${key}: ${msgs[0]}`);
            }
          }
          if (errorDetails.length > 0) {
            message = errorDetails.slice(0, 3).join(' | ');
          }
        }

        if (problem.traceId) {
          message += ` (Trace ID: ${problem.traceId})`;
        }
      }

      // 3. Status-specific semantic handling
      if (error.status === 0) {
        title = 'সংযোগ ত্রুটি (Network Offline)';
        message = 'সার্ভারের সাথে সংযোগ স্থাপন করা যায়নি। অনুগ্রহ করে চেক করুন ব্যাকএন্ড চালু আছে কিনা।';
      } else if (error.status === 403) {
        isWarning = true;
        title = 'অননুমোদিত অ্যাকশন (403 Forbidden)';
        const role = context.userRole() || 'Unknown';
        message = message && message !== 'অনুরোধটি সম্পন্ন করা যায়নি। অনুগ্রহ করে পুনরায় চেষ্টা করুন।'
          ? message
          : `আপনার বর্তমান ভূমিকা (${role})-র এই সংবিধিবদ্ধ কার্যক্রমটি সম্পন্ন করার অনুমতি নেই। (Access Denied)`;
      } else if (error.status === 404) {
        isWarning = true;
        title = 'রেকর্ড পাওয়া যায়নি (404 Not Found)';
        message = message || 'অনুরোধকৃত চালান বা তথ্য সার্ভারে পাওয়া যায়নি।';
      } else if (error.status === 409) {
        isWarning = true;
        title = 'কনকারেন্সি সংঘাত (409 Conflict)';
        message = 'ডকুমেন্টটি ইতিমধ্যে অন্য ব্যবহারকারী দ্বারা পরিবর্তিত হয়েছে। পৃষ্ঠাটি রিফ্রেশ করে পুনরায় চেষ্টা করুন।';
      } else if (error.status === 422) {
        title = 'সংবিধিবদ্ধ যাচাইকরণ ব্যর্থ (422 Statutory Validation)';
        message = message || 'এনবিআর সংবিধিবদ্ধ নিয়মানুযায়ী ডাটা যাচাইকরণ ব্যর্থ হয়েছে।';
      } else if (error.status >= 500) {
        title = `সার্ভার ত্রুটি (${error.status} Internal Error)`;
      }

      // Show user-friendly notification
      if (isWarning) {
        toast.warning(message, title);
      } else {
        toast.error(message, title);
      }

      return throwError(() => error);
    })
  );
};

/**
 * Handles 401 Unauthorized with token refresh and request replay queue
 */
function handle401Error(
  req: HttpRequest<unknown>,
  next: HttpHandlerFn,
  error: HttpErrorResponse,
  authService: AuthService,
  router: Router,
  toast: ToastService
): Observable<HttpEvent<unknown>> {
  authService.handleSessionExpired(router.url);
  return throwError(() => error);
}
