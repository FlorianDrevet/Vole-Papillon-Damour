import {
  computed,
  inject,
  Injectable,
  InjectionToken,
  PLATFORM_ID,
  signal,
} from '@angular/core';
import {isPlatformBrowser} from '@angular/common';
import type {
  AccountInfo,
  AuthenticationResult,
  IPublicClientApplication,
  RedirectRequest,
} from '@azure/msal-browser';

import {
  catalogLoginRequest,
  catalogMsalConfig,
  catalogRegistrationRequest,
} from './catalog-auth.config';

export type CatalogMsalModule = Pick<
  typeof import('@azure/msal-browser'),
  'PublicClientApplication' | 'InteractionRequiredAuthError'
>;

export type CatalogMsalLoader = () => Promise<CatalogMsalModule>;

export const CATALOG_MSAL_LOADER = new InjectionToken<CatalogMsalLoader>(
  'CATALOG_MSAL_LOADER',
  {
    providedIn: 'root',
    factory: () => () => import('@azure/msal-browser'),
  },
);

const ADMINISTRATION_ROUTE = '/administration';
// Public pages must not wait on a hidden Entra iframe: past this delay the
// cached session is treated as gone, like an explicit expiry.
export const CATALOG_SILENT_SESSION_TIMEOUT_MS = 5000;
const SESSION_RESUME_ATTEMPT_KEY = 'catalog-auth-session-resume-attempted';
const ADMINISTRATION_ROLES = new Set(['administration', 'admin']);
const VOLUNTEER_ROLES = new Set(['tri', 'caisse']);
const INTERACTION_REQUIRED_ERROR_CODES = new Set([
  'interaction_required',
  'consent_required',
  'login_required',
  'bad_token',
  'ui_not_allowed',
  'interrupted_user',
  'no_tokens_found',
  'refresh_token_expired',
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

export class CatalogAuthenticationRedirectStartedError extends Error {
  constructor() {
    super('An interactive authentication redirect has been started.');
    this.name = 'CatalogAuthenticationRedirectStartedError';
  }
}

export type CatalogAuthState =
  | 'initializing'
  | 'authenticated'
  | 'reauthentication-required'
  | 'signed-out';

@Injectable({providedIn: 'root'})
export class CatalogAuthService {
  private readonly platformId = inject(PLATFORM_ID);
  private readonly loadMsal = inject(CATALOG_MSAL_LOADER);
  private readonly _account = signal<AccountInfo | null>(null);
  private readonly _initialized = signal(false);
  private readonly _state = signal<CatalogAuthState>('initializing');
  private readonly _error = signal<string | null>(null);
  private readonly _roles = signal<readonly string[]>([]);
  private readonly _recognizedAccount = signal<AccountInfo | null>(null);

  private client: IPublicClientApplication | null = null;
  private msal: CatalogMsalModule | null = null;
  private initialization: Promise<void> | null = null;

  readonly account = this._account.asReadonly();
  readonly initialized = this._initialized.asReadonly();
  readonly state = this._state.asReadonly();
  readonly requiresReauthentication = computed(
    () => this._state() === 'reauthentication-required',
  );
  readonly isAuthenticated = computed(
    () => this._state() === 'authenticated' && this._account() !== null,
  );
  readonly roles = this._roles.asReadonly();
  // This signal controls navigation affordances only; API policies remain authoritative.
  readonly isAdministrator = computed(() =>
    this._roles().some(role => ADMINISTRATION_ROLES.has(role.trim().toLowerCase())),
  );
  readonly isVolunteer = computed(() =>
    this._roles().some(role => VOLUNTEER_ROLES.has(role.trim().toLowerCase())),
  );
  readonly error = this._error.asReadonly();
  /**
   * Account found in the browser cache whose session could not be restored on
   * load. It is only a login hint: it never grants the signed-in UI.
   */
  readonly recognizedAccount = this._recognizedAccount.asReadonly();

  initialize(): Promise<void> {
    if (this.initialization) {
      return this.initialization;
    }

    if (!isPlatformBrowser(this.platformId)) {
      this._state.set('signed-out');
      this._initialized.set(true);
      this.initialization = Promise.resolve();
      return this.initialization;
    }

    this._initialized.set(false);
    this._state.set('initializing');
    this._error.set(null);
    this.initialization = this.initializeBrowser();
    return this.initialization;
  }

  async login(startPage: string = ADMINISTRATION_ROUTE): Promise<void> {
    const loginHint = this._recognizedAccount()?.username;
    await this.startInteractiveLogin(
      loginHint ? {...catalogLoginRequest, loginHint} : catalogLoginRequest,
      startPage,
    );
  }

  /**
   * Sends a recognized visitor straight to the login page, once per browser
   * session so a login that does not restore the session cannot loop.
   */
  async resumeRecognizedSession(startPage: string): Promise<boolean> {
    await this.initialize();
    if (this.isAuthenticated() || !this._recognizedAccount() || readResumeAttempted()) {
      return false;
    }

    writeResumeAttempted(true);
    await this.login(startPage);
    return true;
  }

  async register(startPage: string = '/compte'): Promise<void> {
    await this.startInteractiveLogin(catalogRegistrationRequest, startPage);
  }

  private async startInteractiveLogin(request: RedirectRequest, startPage: string): Promise<void> {
    await this.initialize();
    const client = this.requireClient();

    await client.loginRedirect({
      ...request,
      redirectStartPage: new URL(startPage, window.location.origin).href,
    });
  }

  async logout(): Promise<void> {
    await this.initialize();
    const client = this.requireClient();
    const account = this._account();

    this._account.set(null);
    this._recognizedAccount.set(null);
    this._roles.set([]);
    this._state.set('signed-out');
    writeResumeAttempted(false);

    await client.logoutRedirect({
      account: account ?? undefined,
    });
  }

  async getApiAccessToken(): Promise<string> {
    await this.initialize();
    const client = this.requireClient();
    const account = this._account();

    if (!account) {
      this._state.set('signed-out');
      throw new Error('No active Entra account is available.');
    }

    try {
      const result = await client.acquireTokenSilent({
        account,
        scopes: catalogLoginRequest.scopes,
      });
      this.setTokenClaims(result.idTokenClaims as AccountInfo['idTokenClaims']);
      this._roles.set(readRoles(result.accessToken));
      this._state.set('authenticated');
      return result.accessToken;
    } catch (error: unknown) {
      if (isInteractionRequiredError(this.msal, error)) {
        this.markSessionRequiresReauthentication();
        await client.acquireTokenRedirect({
          ...catalogLoginRequest,
          account,
          redirectStartPage: window.location.href,
        });
        throw new CatalogAuthenticationRedirectStartedError();
      }

      throw error;
    }
  }

  async refreshApiAccessToken(): Promise<string | null> {
    try {
      await this.initialize();
      const account = this._account();
      if (!account) {
        this._state.set('signed-out');
        return null;
      }

      const result = await this.requireClient().acquireTokenSilent({
        account,
        scopes: catalogLoginRequest.scopes,
        forceRefresh: true,
      });
      this.setTokenClaims(result.idTokenClaims as AccountInfo['idTokenClaims']);
      this._roles.set(readRoles(result.accessToken));
      this._state.set('authenticated');
      return result.accessToken;
    } catch (error: unknown) {
      if (isInteractionRequiredError(this.msal, error)) {
        this.markSessionRequiresReauthentication();
      }

      return null;
    }
  }

  markSessionRequiresReauthentication(): void {
    this._roles.set([]);
    this._state.set(this._account() ? 'reauthentication-required' : 'signed-out');
  }

  async tryGetApiAccessToken(): Promise<string | null> {
    try {
      await this.initialize();
      const account = this._account();
      if (!account) {
        return null;
      }

      const result = await this.requireClient().acquireTokenSilent({
        account,
        scopes: catalogLoginRequest.scopes,
      });
      this.setTokenClaims(result.idTokenClaims as AccountInfo['idTokenClaims']);
      this._roles.set(readRoles(result.accessToken));
      this._state.set('authenticated');
      return result.accessToken;
    } catch {
      // Passive checks must never start an interactive redirect or surface an auth failure.
      return null;
    }
  }

  private async initializeBrowser(): Promise<void> {
    let succeeded = false;

    try {
      const msal = await this.loadMsal();
      this.msal = msal;
      this.client = new msal.PublicClientApplication(catalogMsalConfig);
      await this.client.initialize();

      const result: AuthenticationResult | null = await this.client.handleRedirectPromise();
      if (result?.account) {
        this.client.setActiveAccount(result.account);
      }

      this.syncFromCache(result?.idTokenClaims as AccountInfo['idTokenClaims']);
      if (!result?.idTokenClaims) {
        await this.restoreCachedSession();
      }
      if (result?.accessToken && this._account()) {
        this._roles.set(readRoles(result.accessToken));
        this._state.set('authenticated');
      }
      if (this._state() === 'authenticated') {
        writeResumeAttempted(false);
      }
      succeeded = true;
    } catch {
      // A configuration/network failure must not make the public SSR catalogue
      // unavailable. The administration page exposes this fixed, non-sensitive
      // message and allows the user to retry after the deployment is corrected.
      this._error.set('La connexion à l’administration est momentanément indisponible.');
      this._state.set('signed-out');
    } finally {
      this._initialized.set(true);
      if (!succeeded) {
        this.initialization = null;
      }
    }
  }

  private async restoreCachedSession(): Promise<void> {
    const account = this._account();
    if (!account) {
      return;
    }

    try {
      const result = await withTimeout(
        this.requireClient().acquireTokenSilent({
          account,
          scopes: catalogLoginRequest.scopes,
        }),
        CATALOG_SILENT_SESSION_TIMEOUT_MS,
      );
      this.setTokenClaims(result.idTokenClaims as AccountInfo['idTokenClaims']);
      this._roles.set(readRoles(result.accessToken));
      this._recognizedAccount.set(null);
      this._state.set('authenticated');
    } catch {
      // Whatever the cause (expired refresh token, blocked iframe, timeout),
      // an unverified session must not be shown as signed in.
      this._account.set(null);
      this._roles.set([]);
      this._recognizedAccount.set(account);
      this._state.set('signed-out');
    }
  }

  private syncFromCache(idTokenClaims?: AccountInfo['idTokenClaims']): void {
    const client = this.requireClient();
    const activeAccount = client.getActiveAccount();
    const active = activeAccount ?? client.getAllAccounts()[0] ?? null;

    if (active && !activeAccount) {
      client.setActiveAccount(active);
    }

    this._account.set(active && idTokenClaims ? {...active, idTokenClaims} : active);
    this._roles.set([]);
    this._state.set(
      active ? (idTokenClaims ? 'authenticated' : 'initializing') : 'signed-out',
    );
  }

  private setTokenClaims(idTokenClaims: AccountInfo['idTokenClaims']): void {
    const account = this._account();
    if (account && idTokenClaims) {
      this._account.set({...account, idTokenClaims});
    }
  }

  private requireClient(): IPublicClientApplication {
    if (!this.client) {
      throw new Error('The browser authentication client is not available.');
    }

    return this.client;
  }
}

function withTimeout<T>(operation: Promise<T>, timeoutMs: number): Promise<T> {
  let timer: ReturnType<typeof setTimeout> | undefined;
  const timeout = new Promise<never>((_, reject) => {
    timer = setTimeout(
      () => reject(new Error('The silent session restore timed out.')),
      timeoutMs,
    );
  });

  return Promise.race([operation, timeout]).finally(() => clearTimeout(timer));
}

function readResumeAttempted(): boolean {
  try {
    return globalThis.sessionStorage?.getItem(SESSION_RESUME_ATTEMPT_KEY) === 'true';
  } catch {
    return false;
  }
}

function writeResumeAttempted(attempted: boolean): void {
  try {
    if (attempted) {
      globalThis.sessionStorage?.setItem(SESSION_RESUME_ATTEMPT_KEY, 'true');
    } else {
      globalThis.sessionStorage?.removeItem(SESSION_RESUME_ATTEMPT_KEY);
    }
  } catch {
    // Storage can be unavailable (private mode); the guard then simply does not persist.
  }
}

function readRoles(accessToken: string): string[] {
  const tokenParts = accessToken.split('.');
  if (tokenParts.length < 2 || typeof globalThis.atob !== 'function') {
    return [];
  }

  let claims: Record<string, unknown>;
  try {
    const encodedPayload = tokenParts[1]
      .replace(/-/g, '+')
      .replace(/_/g, '/')
      .padEnd(tokenParts[1].length + (4 - tokenParts[1].length % 4) % 4, '=');
    claims = JSON.parse(globalThis.atob(encodedPayload)) as Record<string, unknown>;
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

function isInteractionRequiredError(
  msal: CatalogMsalModule | null,
  error: unknown,
): boolean {
  if (msal && error instanceof msal.InteractionRequiredAuthError) {
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
