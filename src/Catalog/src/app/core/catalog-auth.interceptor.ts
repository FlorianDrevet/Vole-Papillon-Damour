import {
  HttpErrorResponse,
  HttpInterceptorFn,
  HttpContextToken,
} from '@angular/common/http';
import {inject} from '@angular/core';
import {catchError, from, switchMap, throwError} from 'rxjs';

import {CatalogAuthService} from './catalog-auth.service';

export const CATALOG_AUTH_RETRIED = new HttpContextToken<boolean>(() => false);

export const catalogAuthInterceptor: HttpInterceptorFn = (request, next) => {
  const auth = inject(CatalogAuthService);

  // Public catalog requests must stay public: a 401 without a bearer token is
  // a server/API concern, not a reason to start or refresh a browser session.
  if (!request.headers.has('Authorization')) {
    return next(request);
  }

  if (request.context.get(CATALOG_AUTH_RETRIED)) {
    return next(request).pipe(
      catchError(error => {
        if (isUnauthorized(error)) {
          auth.markSessionRequiresReauthentication();
        }

        return throwError(() => error);
      }),
    );
  }

  return next(request).pipe(
    catchError(error => {
      if (!isUnauthorized(error)) {
        return throwError(() => error);
      }

      return from(auth.refreshApiAccessToken()).pipe(
        switchMap(token => {
          if (!token) {
            return throwError(() => error);
          }

          return next(request.clone({
            setHeaders: {Authorization: `Bearer ${token}`},
            context: request.context.set(CATALOG_AUTH_RETRIED, true),
          }));
        }),
        catchError(retryError => {
          if (isUnauthorized(retryError)) {
            auth.markSessionRequiresReauthentication();
          }

          return throwError(() => retryError);
        }),
      );
    }),
  );
};

function isUnauthorized(error: unknown): error is HttpErrorResponse {
  return error instanceof HttpErrorResponse && error.status === 401;
}
