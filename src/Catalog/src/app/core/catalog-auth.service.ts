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
const ADMINISTRATION_ROLES = new Set(['administration', 'admin']);

export class CatalogAuthenticationRedirectStartedError extends Error {
  constructor() {
    super('An interactive authentication redirect has been started.');
    this.name = 'CatalogAuthenticationRedirectStartedError';
  }
}

@Injectable({providedIn: 'root'})
export class CatalogAuthService {
  private readonly platformId = inject(PLATFORM_ID);
  private readonly loadMsal = inject(CATALOG_MSAL_LOADER);
  private readonly _account = signal<AccountInfo | null>(null);
  private readonly _initialized = signal(false);
  private readonly _error = signal<string | null>(null);
  private readonly _roles = signal<readonly string[]>([]);

  private client: IPublicClientApplication | null = null;
  private msal: CatalogMsalModule | null = null;
  private initialization: Promise<void> | null = null;

  readonly account = this._account.asReadonly();
  readonly initialized = this._initialized.asReadonly();
  readonly isAuthenticated = computed(() => this._account() !== null);
  readonly roles = this._roles.asReadonly();
  // This signal controls navigation affordances only; API policies remain authoritative.
  readonly isAdministrator = computed(() =>
    this._roles().some(role => ADMINISTRATION_ROLES.has(role.trim().toLowerCase())),
  );
  readonly error = this._error.asReadonly();

  initialize(): Promise<void> {
    if (this.initialization) {
      return this.initialization;
    }

    if (!isPlatformBrowser(this.platformId)) {
      this._initialized.set(true);
      this.initialization = Promise.resolve();
      return this.initialization;
    }

    this._initialized.set(false);
    this._error.set(null);
    this.initialization = this.initializeBrowser();
    return this.initialization;
  }

  async login(startPage: string = ADMINISTRATION_ROUTE): Promise<void> {
    await this.startInteractiveLogin(catalogLoginRequest, startPage);
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

    await client.logoutRedirect({
      account: this._account() ?? undefined,
    });
  }

  async getApiAccessToken(): Promise<string> {
    await this.initialize();
    const client = this.requireClient();
    const account = this._account();

    if (!account) {
      throw new Error('No active Entra account is available.');
    }

    try {
      const result = await client.acquireTokenSilent({
        account,
        scopes: catalogLoginRequest.scopes,
      });
      this._roles.set(readRoles(result.accessToken));
      return result.accessToken;
    } catch (error: unknown) {
      if (this.msal && error instanceof this.msal.InteractionRequiredAuthError) {
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

      this.syncFromCache();
      if (result?.accessToken) {
        this._roles.set(readRoles(result.accessToken));
      }
      succeeded = true;
    } catch {
      // A configuration/network failure must not make the public SSR catalogue
      // unavailable. The administration page exposes this fixed, non-sensitive
      // message and allows the user to retry after the deployment is corrected.
      this._error.set('La connexion à l’administration est momentanément indisponible.');
    } finally {
      this._initialized.set(true);
      if (!succeeded) {
        this.initialization = null;
      }
    }
  }

  private syncFromCache(): void {
    const client = this.requireClient();
    const activeAccount = client.getActiveAccount();
    const active = activeAccount ?? client.getAllAccounts()[0] ?? null;

    if (active && !activeAccount) {
      client.setActiveAccount(active);
    }

    this._account.set(active);
    this._roles.set([]);
  }

  private requireClient(): IPublicClientApplication {
    if (!this.client) {
      throw new Error('The browser authentication client is not available.');
    }

    return this.client;
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
