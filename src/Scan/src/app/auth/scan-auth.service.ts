import {Injectable} from '@angular/core';
import {MsalBroadcastService, MsalService} from '@azure/msal-angular';
import {AccountInfo, AuthenticationResult, EventType} from '@azure/msal-browser';
import {BehaviorSubject, defer, Observable} from 'rxjs';
import {filter} from 'rxjs/operators';

import {loginRequest} from './msal-config';

export const SCAN_REQUIRED_ROLE = 'Tri ou Caisse';
export const SCAN_TRI_ROLE = 'Tri';
export const SCAN_CASH_ROLE = 'Caisse';

export type ScanAuthStatus =
  | 'checking'
  | 'unauthenticated'
  | 'unauthorized'
  | 'authorized'
  | 'degraded';

export interface ScanAuthState {
  status: ScanAuthStatus;
  account: AccountInfo | null;
  roles: readonly string[];
  requiredRole: string;
}

@Injectable({providedIn: 'root'})
export class ScanAuthService {
  private static readonly localAuthorizationStorageKey = 'vpd-scan-local-authorization';
  private readonly accountSubject = new BehaviorSubject<AccountInfo | null>(null);
  private readonly authStateSubject = new BehaviorSubject<ScanAuthState>({
    status: 'checking',
    account: null,
    roles: [],
    requiredRole: SCAN_REQUIRED_ROLE,
  });
  private authorizationCheck = 0;

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
          message.eventType === EventType.LOGOUT_SUCCESS ||
          message.eventType === EventType.LOGOUT_FAILURE
        ) {
          this.publishAccount(null);
          return;
        }

        if (message.eventType === EventType.ACQUIRE_TOKEN_FAILURE) {
          this.handleSilentTokenFailure();
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

  get canSort(): boolean {
    return hasRole(this.roles, SCAN_TRI_ROLE);
  }

  get canSell(): boolean {
    return hasRole(this.roles, SCAN_CASH_ROLE);
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
    this.forgetLocalAuthorization(this.accountSubject.value);
    this.publishAccount(null);
    this.msalService.logoutRedirect().subscribe({
      error: () => undefined,
    });
  }

  handleServerAuthorizationFailure(): void {
    this.forgetLocalAuthorization(this.accountSubject.value);
    this.publishAccount(null);
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
    const check = ++this.authorizationCheck;

    if (account === null) {
      this.accountSubject.next(null);
      this.authStateSubject.next({
        status: 'unauthenticated',
        account: null,
        roles: [],
        requiredRole: SCAN_REQUIRED_ROLE,
      });
      return;
    }

    this.accountSubject.next(account);
    this.authStateSubject.next({
      status: 'checking',
      account,
      roles: [],
      requiredRole: SCAN_REQUIRED_ROLE,
    });

    this.msalService.acquireTokenSilent({
      account,
      scopes: loginRequest.scopes,
    }).subscribe({
      next: result => {
        if (check !== this.authorizationCheck) {
          return;
        }

        const roles = readRoles(result.accessToken);
        const status: ScanAuthStatus = roles.some(role =>
          role.toLowerCase() === SCAN_TRI_ROLE.toLowerCase() ||
          role.toLowerCase() === SCAN_CASH_ROLE.toLowerCase())
          ? 'authorized'
          : 'unauthorized';

        if (status === 'authorized') {
          this.rememberLocalAuthorization(account, roles);
        } else {
          this.forgetLocalAuthorization(account);
        }

        this.authStateSubject.next({
          status,
          account,
          roles,
          requiredRole: SCAN_REQUIRED_ROLE,
        });
      },
      error: () => {
        if (check === this.authorizationCheck) {
          this.publishDegradedAccount(account);
        }
      },
    });
  }

  private handleSilentTokenFailure(): void {
    const account = this.accountSubject.value;
    if (!account) {
      this.publishAccount(null);
      return;
    }

    this.publishDegradedAccount(account);
  }

  private publishDegradedAccount(account: AccountInfo): void {
    const roles = this.getLocalAuthorizationRoles(account);
    if (!roles || !hasScanRole(roles)) {
      this.publishAccount(null);
      return;
    }

    this.authorizationCheck += 1;
    this.accountSubject.next(account);
    this.authStateSubject.next({
      status: 'degraded',
      account,
      roles,
      requiredRole: SCAN_REQUIRED_ROLE,
    });
  }

  private rememberLocalAuthorization(account: AccountInfo, roles: readonly string[]): void {
    const record: LocalAuthorizationRecord = {
      homeAccountId: account.homeAccountId,
      roles: [...roles],
    };

    try {
      if (typeof localStorage !== 'undefined') {
        localStorage.setItem(
          ScanAuthService.localAuthorizationStorageKey,
          JSON.stringify(record),
        );
      }
    } catch {
      // The in-memory auth state still protects the current page if storage is unavailable.
    }
  }

  private getLocalAuthorizationRoles(account: AccountInfo): string[] | null {
    const currentState = this.authStateSubject.value;
    if (
      currentState.account?.homeAccountId === account.homeAccountId &&
      hasScanRole(currentState.roles)
    ) {
      return [...currentState.roles];
    }

    try {
      if (typeof localStorage === 'undefined') {
        return null;
      }

      const rawRecord = localStorage.getItem(ScanAuthService.localAuthorizationStorageKey);
      if (!rawRecord) {
        return null;
      }

      const record = JSON.parse(rawRecord) as Partial<LocalAuthorizationRecord>;
      return record.homeAccountId === account.homeAccountId && Array.isArray(record.roles)
        ? record.roles.filter((role): role is string => typeof role === 'string')
        : null;
    } catch {
      return null;
    }
  }

  private forgetLocalAuthorization(account: AccountInfo | null): void {
    try {
      if (typeof localStorage === 'undefined') {
        return;
      }

      const rawRecord = localStorage.getItem(ScanAuthService.localAuthorizationStorageKey);
      if (!account || !rawRecord) {
        if (!account) {
          localStorage.removeItem(ScanAuthService.localAuthorizationStorageKey);
        }
        return;
      }

      const record = JSON.parse(rawRecord) as Partial<LocalAuthorizationRecord>;
      if (record.homeAccountId === account.homeAccountId) {
        localStorage.removeItem(ScanAuthService.localAuthorizationStorageKey);
      }
    } catch {
      // Ignore storage failures; the server remains the authority for online actions.
    }
  }
}

interface LocalAuthorizationRecord {
  homeAccountId: string;
  roles: string[];
}

function hasRole(roles: readonly string[], expectedRole: string): boolean {
  return roles.some(role => role.toLowerCase() === expectedRole.toLowerCase());
}

function hasScanRole(roles: readonly string[]): boolean {
  return hasRole(roles, SCAN_TRI_ROLE) || hasRole(roles, SCAN_CASH_ROLE);
}

function readRoles(accessToken: string): string[] {
  const tokenParts = accessToken.split('.');
  if (tokenParts.length < 2) {
    return [];
  }

  let claims: Record<string, unknown>;
  try {
    const base64Payload = tokenParts[1]
      .replace(/-/g, '+')
      .replace(/_/g, '/')
      .padEnd(tokenParts[1].length + (4 - tokenParts[1].length % 4) % 4, '=');
    claims = JSON.parse(atob(base64Payload)) as Record<string, unknown>;
  } catch {
    return [];
  }

  const rawRoles = claims['roles'];

  if (typeof rawRoles === 'string') {
    return [rawRoles];
  }

  return Array.isArray(rawRoles)
    ? rawRoles.filter((role): role is string => typeof role === 'string')
    : [];
}
