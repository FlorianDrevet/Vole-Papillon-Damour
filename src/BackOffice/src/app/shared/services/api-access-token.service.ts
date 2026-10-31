import {Injectable} from '@angular/core';
import {MsalBroadcastService, MsalService} from '@azure/msal-angular';
import {AccountInfo, InteractionRequiredAuthError, InteractionStatus} from '@azure/msal-browser';
import {catchError, filter, from, Observable, switchMap, take, throwError} from 'rxjs';
import {map} from 'rxjs/operators';

import {environment} from '../../../environments/environment';
import {loginRequest} from '../auth/msal-config';
import {KeycloakAuthService} from '../auth/keycloak-auth.service';

const INTERACTION_REQUIRED_ERROR_CODES = new Set([
  'interaction_required',
  'consent_required',
  'login_required',
  'bad_token',
  'ui_not_allowed',
  'interrupted_user',
  'no_tokens_found',
  'refresh_token_expired',
  // Silent iframe failures leave a cached account present but cannot produce
  // an API token. A top-level redirect is the only reliable recovery path.
  'monitor_window_timeout',
  'block_iframe_reload',
]);

const INTERACTION_REQUIRED_SUBERRORS = new Set([
  'message_only',
  'additional_action',
  'basic_action',
  'user_password_expired',
  'consent_required',
  'bad_token',
  'ui_not_allowed',
  'interrupted_user',
]);

@Injectable({
  providedIn: 'root',
})
export class ApiAccessTokenService {
  private interactiveRedirectStarted = false;

  constructor(
    private readonly msalService: MsalService,
    private readonly msalBroadcastService: MsalBroadcastService,
    private readonly keycloakAuthService: KeycloakAuthService,
  ) {}

  public getApiAccessToken$(forceRefresh = false): Observable<string> {
    if (environment.auth_provider === 'keycloak') {
      return from(this.keycloakAuthService.getApiAccessToken(forceRefresh));
    }

    return this.msalBroadcastService.inProgress$.pipe(
      filter(status => status === InteractionStatus.None),
      take(1),
      switchMap(() => {
        const account = this.getActiveAccount();

        if (!account) {
          return throwError(() => new Error('No active Entra account is available.'));
        }

        return this.msalService.acquireTokenSilent({
          account,
          scopes: [environment.entra.apiScope],
          ...(forceRefresh ? {forceRefresh: true} : {}),
        }).pipe(
          map(result => result.accessToken),
          catchError((error: unknown) => {
            // Le compte est encore en cache mais Entra refuse de renouveler le jeton
            // sans intervention (session expirée, consentement révoqué, MFA demandé).
            // Renvoyer sur l'écran de connexion ne débloquerait rien — le compte y est
            // toujours vu comme valide : seule une redirection interactive rétablit la
            // session, on la déclenche donc directement.
            if (isInteractionRequiredError(error) && !this.interactiveRedirectStarted) {
              this.interactiveRedirectStarted = true;
              this.msalService
                .acquireTokenRedirect({
                  ...loginRequest,
                  account,
                  redirectStartPage: window.location.href,
                })
                .subscribe({
                  error: () => {
                    // Allow the retry action on the page to start a new redirect if
                    // MSAL rejected the first one before navigating away.
                    this.interactiveRedirectStarted = false;
                  },
                });
            }

            return throwError(() => error);
          }),
        );
      }),
    );
  }

  private getActiveAccount(): AccountInfo | null {
    const activeAccount = this.msalService.instance.getActiveAccount();
    if (activeAccount) {
      return activeAccount;
    }

    const cachedAccounts = this.msalService.instance.getAllAccounts();
    if (cachedAccounts.length !== 1) {
      return null;
    }

    const onlyAccount = cachedAccounts[0];
    this.msalService.instance.setActiveAccount(onlyAccount);
    return onlyAccount;
  }
}

function isInteractionRequiredError(error: unknown): boolean {
  if (error instanceof InteractionRequiredAuthError) {
    return true;
  }

  const errorCode = readErrorProperty(error, 'errorCode');
  const subError = readErrorProperty(error, 'subError');
  if (errorCode && INTERACTION_REQUIRED_ERROR_CODES.has(errorCode)) {
    return true;
  }

  if (subError && INTERACTION_REQUIRED_SUBERRORS.has(subError)) {
    return true;
  }

  const message = readErrorProperty(error, 'message');
  return message
    ? [...INTERACTION_REQUIRED_ERROR_CODES].some(code => message.includes(code))
    : false;
}

function readErrorProperty(error: unknown, property: string): string | null {
  if (!error || typeof error !== 'object') {
    return null;
  }

  const value = (error as Record<string, unknown>)[property];
  return typeof value === 'string' ? value.trim().toLowerCase() : null;
}
