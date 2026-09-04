import {Injectable} from '@angular/core';
import {MsalBroadcastService, MsalService} from '@azure/msal-angular';
import {AccountInfo, AuthenticationResult, EventType} from '@azure/msal-browser';
import {BehaviorSubject, defer, Observable} from 'rxjs';
import {filter} from 'rxjs/operators';

import {loginRequest} from './msal-config';

export const SCAN_REQUIRED_ROLE = 'Tri';

export type ScanAuthStatus = 'checking' | 'unauthenticated' | 'unauthorized' | 'authorized';

export interface ScanAuthState {
  status: ScanAuthStatus;
  account: AccountInfo | null;
  roles: readonly string[];
  requiredRole: string;
}

@Injectable({providedIn: 'root'})
export class ScanAuthService {
  private readonly accountSubject = new BehaviorSubject<AccountInfo | null>(null);
  private readonly authStateSubject = new BehaviorSubject<ScanAuthState>({
    status: 'checking',
    account: null,
    roles: [],
    requiredRole: SCAN_REQUIRED_ROLE,
  });

  readonly account$ = this.accountSubject.asObservable();
  readonly authState$ = this.authStateSubject.asObservable();

  constructor(
    private readonly msalService: MsalService,
    private readonly msalBroadcastService: MsalBroadcastService,
  ) {
    this.publishCachedAccount();

    this.msalBroadcastService.msalSubject$
      .pipe(filter(message =>
        message.eventType === EventType.LOGIN_SUCCESS ||
        message.eventType === EventType.ACQUIRE_TOKEN_FAILURE ||
        message.eventType === EventType.LOGOUT_SUCCESS ||
        message.eventType === EventType.LOGOUT_FAILURE ||
        message.eventType === EventType.ACTIVE_ACCOUNT_CHANGED))
      .subscribe(message => {
        if (message.eventType === EventType.LOGIN_SUCCESS) {
          const result = message.payload as AuthenticationResult;
          if (result.account) {
            this.msalService.instance.setActiveAccount(result.account);
            this.publishAccount(result.account);
            return;
          }
        }

        if (
          message.eventType === EventType.ACQUIRE_TOKEN_FAILURE ||
          message.eventType === EventType.LOGOUT_SUCCESS ||
          message.eventType === EventType.LOGOUT_FAILURE
        ) {
          this.publishAccount(null);
          return;
        }

        this.publishCachedAccount();
      });
  }

  get isAuthenticated(): boolean {
    return this.accountSubject.value !== null;
  }

  get isAuthorized(): boolean {
    return this.authStateSubject.value.status === 'authorized';
  }

  get authState(): ScanAuthState {
    return this.authStateSubject.value;
  }

  get roles(): readonly string[] {
    return this.authStateSubject.value.roles;
  }

  get displayName(): string | null {
    const account = this.accountSubject.value;
    return account?.name || account?.username || null;
  }

  login(startPage = '/'): Observable<void> {
    return defer(() => this.msalService.loginRedirect({
      ...loginRequest,
      redirectStartPage: new URL(startPage, window.location.origin).href,
    }));
  }

  logout(): void {
    this.publishAccount(null);
    this.msalService.logoutRedirect().subscribe({
      error: () => undefined,
    });
  }

  private publishCachedAccount(): void {
    const activeAccount = this.msalService.instance.getActiveAccount();
    if (activeAccount) {
      this.publishAccount(activeAccount);
      return;
    }

    const firstAccount = this.msalService.instance.getAllAccounts()[0] ?? null;
    if (firstAccount) {
      this.msalService.instance.setActiveAccount(firstAccount);
    }
    this.publishAccount(firstAccount);
  }

  private publishAccount(account: AccountInfo | null): void {
    const roles = account ? readRoles(account) : [];
    const status: ScanAuthStatus = account === null
      ? 'unauthenticated'
      : roles.some(role => role.toLowerCase() === SCAN_REQUIRED_ROLE.toLowerCase())
        ? 'authorized'
        : 'unauthorized';

    this.accountSubject.next(account);
    this.authStateSubject.next({
      status,
      account,
      roles,
      requiredRole: SCAN_REQUIRED_ROLE,
    });
  }
}

function readRoles(account: AccountInfo): string[] {
  const claims = account.idTokenClaims as Record<string, unknown> | undefined;
  const rawRoles = claims?.['roles']
    ?? claims?.['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'];

  if (typeof rawRoles === 'string') {
    return [rawRoles];
  }

  return Array.isArray(rawRoles)
    ? rawRoles.filter((role): role is string => typeof role === 'string')
    : [];
}
