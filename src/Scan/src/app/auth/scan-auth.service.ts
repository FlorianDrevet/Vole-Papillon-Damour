import {Injectable} from '@angular/core';
import {MsalBroadcastService, MsalService} from '@azure/msal-angular';
import {
  AccountInfo,
  AuthenticationResult,
  EventType,
  InteractionRequiredAuthError,
  InteractionStatus,
} from '@azure/msal-browser';
import {BehaviorSubject, defer, Observable} from 'rxjs';
import {filter} from 'rxjs/operators';

import {loginRequest} from './msal-config';

// A degraded session usually self-heals once the network blip that caused the
// silent renewal to fail is over. Retrying a handful of times before asking
// the volunteer to reconnect avoids the previous behaviour where the banner
// stayed up forever until they manually logged out and back in.
const DEGRADED_RETRY_DELAY_MS = 15_000;
const DEGRADED_RETRY_MAX_ATTEMPTS = 5;
// When the refresh token itself is no longer usable, the volunteer is sent
// straight to the sign-in page instead of landing on the home screen with a
// "reconnect" banner. If a previous automatic redirect came back less than
// this long ago without a usable token, fall back to the banner rather than
// bouncing forever between the app and the sign-in page.
const REAUTHENTICATION_LOOP_GUARD_MS = 120_000;

export const SCAN_REQUIRED_ROLE = 'Tri, Caisse ou Livres rares';
export const SCAN_TRI_ROLE = 'Tri';
export const SCAN_CASH_ROLE = 'Caisse';
export const SCAN_RARE_ROLE = 'LivresRares';

export type ScanAuthStatus =
  | 'checking'
  | 'unauthenticated'
  | 'unauthorized'
  | 'authorized'
  | 'degraded'
  | 'reauthenticating';

export interface ScanAuthState {
  status: ScanAuthStatus;
  account: AccountInfo | null;
  roles: readonly string[];
  requiredRole: string;
}

@Injectable({providedIn: 'root'})
export class ScanAuthService {
  private static readonly localAuthorizationStorageKey = 'vpd-scan-local-authorization';
  private static readonly reauthenticationAttemptStorageKey = 'vpd-scan-reauthentication-attempt';
  private readonly accountSubject = new BehaviorSubject<AccountInfo | null>(null);
  private readonly authStateSubject = new BehaviorSubject<ScanAuthState>({
    status: 'checking',
    account: null,
    roles: [],
    requiredRole: SCAN_REQUIRED_ROLE,
  });
  private readonly interactionStatusSubject = new BehaviorSubject<InteractionStatus>(
    InteractionStatus.Startup,
  );
  private authorizationCheck = 0;
  private accountPublicationPending = true;
  private interactionStatus: InteractionStatus = InteractionStatus.Startup;
  private degradedRetryTimer: ReturnType<typeof setTimeout> | null = null;

  readonly account$ = this.accountSubject.asObservable();
  readonly authState$ = this.authStateSubject.asObservable();
  readonly interactionStatus$ = this.interactionStatusSubject.asObservable();

  constructor(
    private readonly msalService: MsalService,
    private readonly msalBroadcastService: MsalBroadcastService,
  ) {
    this.msalBroadcastService.inProgress$.subscribe(status => {
      this.interactionStatus = status;
      this.interactionStatusSubject.next(status);

      if (status === InteractionStatus.None && this.accountPublicationPending) {
        this.accountPublicationPending = false;
        this.publishCachedAccount();
      }
    });

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
            this.requestAccountPublication();
            return;
          }
        }

        if (
          message.eventType === EventType.LOGOUT_SUCCESS ||
          message.eventType === EventType.LOGOUT_FAILURE
        ) {
          this.requestAccountPublication();
          return;
        }

        if (message.eventType === EventType.ACQUIRE_TOKEN_FAILURE) {
          if (this.interactionStatus === InteractionStatus.None) {
            this.handleSilentTokenFailure(message.error);
          }
          return;
        }

        this.requestAccountPublication();
      });

    if (typeof window !== 'undefined') {
      window.addEventListener('online', () => this.retryDegradedSessionNow());
    }
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

  get canManageRareBooks(): boolean {
    return hasRole(this.roles, SCAN_RARE_ROLE);
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
    return defer(() => {
      if (this.interactionStatus !== InteractionStatus.None) {
        throw new Error('interaction_in_progress');
      }

      return this.msalService.loginRedirect({
        ...loginRequest,
        redirectStartPage: new URL(startPage, window.location.origin).href,
      });
    });
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

  private requestAccountPublication(): void {
    this.accountPublicationPending = true;

    if (this.interactionStatus === InteractionStatus.None) {
      this.accountPublicationPending = false;
      this.publishCachedAccount();
    }
  }

  private publishAccount(account: AccountInfo | null): void {
    const check = ++this.authorizationCheck;
    this.clearDegradedRetry();

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
        const status: ScanAuthStatus = hasScanRole(roles) ? 'authorized' : 'unauthorized';

        if (status === 'authorized') {
          this.rememberLocalAuthorization(account, roles);
          this.forgetReauthenticationAttempt();
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
      error: (error: unknown) => {
        if (check === this.authorizationCheck) {
          this.publishDegradedAccount(account, error);
        }
      },
    });
  }

  private handleSilentTokenFailure(error: unknown): void {
    const account = this.accountSubject.value;
    if (!account) {
      this.publishAccount(null);
      return;
    }

    this.publishDegradedAccount(account, error);
  }

  private publishDegradedAccount(account: AccountInfo, error: unknown): void {
    const roles = this.getLocalAuthorizationRoles(account);
    if (!roles || !hasScanRole(roles)) {
      this.publishAccount(null);
      return;
    }

    if (error instanceof InteractionRequiredAuthError && this.tryReauthenticate(account)) {
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

    // A real "you must sign in again" error (expired/consumed refresh token,
    // revoked session…) won't fix itself: retrying would just fail the same
    // way and delay the moment the volunteer is told to reconnect. Anything
    // else (network hiccup, momentary MSAL/iframe issue) is worth retrying
    // silently so the banner clears on its own, the way it does after a
    // manual logout/login round-trip.
    if (!(error instanceof InteractionRequiredAuthError)) {
      this.scheduleDegradedRetry(account, 1);
    }
  }

  private scheduleDegradedRetry(account: AccountInfo, attempt: number): void {
    if (typeof window === 'undefined' || attempt > DEGRADED_RETRY_MAX_ATTEMPTS) {
      return;
    }

    this.clearDegradedRetry();
    this.degradedRetryTimer = setTimeout(
      () => this.retrySilentRenewal(account, attempt),
      DEGRADED_RETRY_DELAY_MS,
    );
  }

  private retryDegradedSessionNow(): void {
    const account = this.accountSubject.value;
    if (!account || this.authStateSubject.value.status !== 'degraded') {
      return;
    }

    this.clearDegradedRetry();
    this.retrySilentRenewal(account, 1);
  }

  private clearDegradedRetry(): void {
    if (this.degradedRetryTimer !== null) {
      clearTimeout(this.degradedRetryTimer);
      this.degradedRetryTimer = null;
    }
  }

  private retrySilentRenewal(account: AccountInfo, attempt: number): void {
    const current = this.authStateSubject.value;
    if (current.status !== 'degraded' || current.account?.homeAccountId !== account.homeAccountId) {
      return;
    }

    const check = ++this.authorizationCheck;
    this.msalService.acquireTokenSilent({
      account,
      scopes: loginRequest.scopes,
    }).subscribe({
      next: result => {
        if (check !== this.authorizationCheck) {
          return;
        }

        const roles = readRoles(result.accessToken);
        const status: ScanAuthStatus = hasScanRole(roles) ? 'authorized' : 'unauthorized';

        if (status === 'authorized') {
          this.rememberLocalAuthorization(account, roles);
          this.forgetReauthenticationAttempt();
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
      error: (error: unknown) => {
        if (check !== this.authorizationCheck) {
          return;
        }

        if (error instanceof InteractionRequiredAuthError) {
          this.tryReauthenticate(account);
          return;
        }

        this.scheduleDegradedRetry(account, attempt + 1);
      },
    });
  }

  /**
   * Sends the volunteer to the sign-in page when the session can only be
   * restored interactively. Offline devices keep the degraded mode so that
   * scanning keeps working; the `online` listener retries and lands here once
   * the network is back. Returns false when the redirect was not started.
   */
  private tryReauthenticate(account: AccountInfo): boolean {
    if (
      typeof window === 'undefined' ||
      (typeof navigator !== 'undefined' && navigator.onLine === false) ||
      this.interactionStatus !== InteractionStatus.None ||
      this.hasRecentReauthenticationAttempt()
    ) {
      return false;
    }

    this.clearDegradedRetry();
    this.authorizationCheck += 1;
    this.rememberReauthenticationAttempt();
    this.accountSubject.next(account);
    this.authStateSubject.next({
      status: 'reauthenticating',
      account,
      roles: [],
      requiredRole: SCAN_REQUIRED_ROLE,
    });

    const {pathname, search, hash} = window.location;
    this.msalService.loginRedirect({
      ...loginRequest,
      loginHint: account.username || undefined,
      redirectStartPage: new URL(`${pathname}${search}${hash}`, window.location.origin).href,
    }).subscribe({
      error: (redirectError: unknown) => this.publishDegradedAccount(account, redirectError),
    });
    return true;
  }

  private hasRecentReauthenticationAttempt(): boolean {
    try {
      const rawAttempt = sessionStorage.getItem(ScanAuthService.reauthenticationAttemptStorageKey);
      const attemptedAt = rawAttempt === null ? Number.NaN : Number(rawAttempt);
      return Number.isFinite(attemptedAt)
        && Date.now() - attemptedAt < REAUTHENTICATION_LOOP_GUARD_MS;
    } catch {
      return false;
    }
  }

  private rememberReauthenticationAttempt(): void {
    try {
      sessionStorage.setItem(ScanAuthService.reauthenticationAttemptStorageKey, String(Date.now()));
    } catch {
      // Without storage the loop guard is best effort; MSAL still refuses concurrent interactions.
    }
  }

  private forgetReauthenticationAttempt(): void {
    try {
      sessionStorage.removeItem(ScanAuthService.reauthenticationAttemptStorageKey);
    } catch {
      // Ignore storage failures.
    }
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
  return hasRole(roles, SCAN_TRI_ROLE)
    || hasRole(roles, SCAN_CASH_ROLE)
    || hasRole(roles, SCAN_RARE_ROLE);
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
