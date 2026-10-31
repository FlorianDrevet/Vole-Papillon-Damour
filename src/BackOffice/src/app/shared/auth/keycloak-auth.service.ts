import {Injectable, signal} from '@angular/core';
import Keycloak, {KeycloakTokenParsed} from 'keycloak-js';

import {environment} from '../../../environments/environment';

const RETURN_URL_STORAGE_KEY = 'vpd.backoffice.keycloak.return-url';

export interface KeycloakAccount {
  name: string | null;
  username: string;
  email: string;
}

/** Browser OIDC session used by the local BackOffice only. */
@Injectable({providedIn: 'root'})
export class KeycloakAuthService {
  readonly account = signal<KeycloakAccount | null>(null);
  readonly isAvailable = signal(true);

  private adapter: Keycloak | null = null;
  private initialization: Promise<void> | null = null;
  private loginRedirect: Promise<void> | null = null;

  initialize(): Promise<void> {
    if (environment.auth_provider !== 'keycloak') {
      return Promise.resolve();
    }

    if (!this.initialization) {
      this.initialization = this.keycloak.init({
        onLoad: 'check-sso',
        pkceMethod: 'S256',
        checkLoginIframe: false,
        silentCheckSsoRedirectUri: `${window.location.origin}/silent-check-sso.html`,
        silentCheckSsoFallback: false,
        scope: 'openid',
      }).then(() => {
        this.syncAccount();
      }).catch(() => {
        // Let the login screen render so it can explain/retry when Aspire or Keycloak
        // is not running. A rejected app initializer would leave a blank page.
        this.isAvailable.set(false);
      });
    }

    return this.initialization;
  }

  async login(startPage: string): Promise<void> {
    if (this.loginRedirect) {
      return this.loginRedirect;
    }

    if (!this.isAvailable()) {
      throw new Error('Keycloak local is unavailable. Start the Aspire Keycloak service and retry.');
    }

    sessionStorage.setItem(RETURN_URL_STORAGE_KEY, normalizeInternalRoute(startPage));
    this.loginRedirect = this.keycloak.login({
      scope: 'openid',
      redirectUri: `${window.location.origin}/`,
    }).catch(error => {
      this.loginRedirect = null;
      throw error;
    });
    return this.loginRedirect;
  }

  async logout(): Promise<void> {
    sessionStorage.removeItem(RETURN_URL_STORAGE_KEY);
    await this.keycloak.logout({redirectUri: `${window.location.origin}/login`});
  }

  async getApiAccessToken(forceRefresh = false): Promise<string> {
    if (!this.keycloak.authenticated) {
      throw new Error('No active Keycloak account is available.');
    }

    try {
      await this.keycloak.updateToken(forceRefresh ? -1 : 30);
    } catch (error) {
      // A rejected refresh token needs a top-level authorization redirect. Preserve
      // the page that made the request so the user returns to it after signing in.
      await this.login(currentInternalRoute());
      throw error;
    }

    const token = this.keycloak.token;
    if (!token) {
      throw new Error('Keycloak did not return an access token.');
    }

    return token;
  }

  consumeReturnUrl(): string | null {
    const returnUrl = sessionStorage.getItem(RETURN_URL_STORAGE_KEY);
    sessionStorage.removeItem(RETURN_URL_STORAGE_KEY);
    return returnUrl ? normalizeInternalRoute(returnUrl) : null;
  }

  async resetSession(): Promise<void> {
    sessionStorage.removeItem(RETURN_URL_STORAGE_KEY);
    await this.keycloak.clearToken();
  }

  private get keycloak(): Keycloak {
    if (!this.adapter) {
      const config = environment.keycloak;
      if (!config) {
        throw new Error('Keycloak is only configured for the local BackOffice environment.');
      }

      this.adapter = new Keycloak(config);
      this.adapter.onAuthSuccess = () => this.syncAccount();
      this.adapter.onAuthRefreshSuccess = () => this.syncAccount();
      this.adapter.onAuthLogout = () => this.account.set(null);
      this.adapter.onTokenExpired = () => {
        void this.adapter?.updateToken(30).then(() => this.syncAccount()).catch(() => this.account.set(null));
      };
    }

    return this.adapter;
  }

  private syncAccount(): void {
    const token = this.keycloak.tokenParsed as KeycloakTokenParsed | undefined;
    if (!this.keycloak.authenticated || !token) {
      this.account.set(null);
      return;
    }

    const email = typeof token['email'] === 'string' ? token['email'] : '';
    const username = typeof token['preferred_username'] === 'string'
      ? token['preferred_username']
      : email;
    const firstName = typeof token['given_name'] === 'string' ? token['given_name'] : '';
    const lastName = typeof token['family_name'] === 'string' ? token['family_name'] : '';
    const name = typeof token['name'] === 'string'
      ? token['name']
      : [firstName, lastName].filter(Boolean).join(' ') || null;

    this.account.set({name, username, email});
  }
}

function currentInternalRoute(): string {
  return `${window.location.pathname}${window.location.search}${window.location.hash}`;
}

function normalizeInternalRoute(value: string): string {
  const route = new URL(value, window.location.origin);
  if (route.origin !== window.location.origin || !value.startsWith('/') || value.startsWith('//')) {
    return '/actualites';
  }

  return `${route.pathname}${route.search}${route.hash}`;
}
