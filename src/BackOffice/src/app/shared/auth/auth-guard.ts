import {Injectable} from '@angular/core';
import {CanActivate, ActivatedRouteSnapshot, Router, RouterStateSnapshot, UrlTree} from '@angular/router';
import {MsalGuard} from '@azure/msal-angular';
import {Observable} from 'rxjs';

import {environment} from '../../../environments/environment';
import {LOGIN_ROUTE} from './msal-config';
import {AuthSessionService} from './auth-session.service';

@Injectable({providedIn: 'root'})
export class BackOfficeAuthGuard implements CanActivate {
  constructor(
    private readonly authSession: AuthSessionService,
    private readonly msalGuard: MsalGuard,
    private readonly router: Router,
  ) {}

  canActivate(
    route: ActivatedRouteSnapshot,
    state: RouterStateSnapshot,
  ): Observable<boolean | UrlTree> | boolean | UrlTree {
    if (environment.auth_provider === 'entra') {
      return this.msalGuard.canActivate(route, state);
    }

    if (this.authSession.isAuthenticated()) {
      return true;
    }

    return this.router.createUrlTree([LOGIN_ROUTE], {queryParams: {returnUrl: state.url}});
  }
}
