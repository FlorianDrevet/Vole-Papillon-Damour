import {computed, inject, Injectable, signal} from '@angular/core';
import {MsalBroadcastService, MsalService} from '@azure/msal-angular';
import {AccountInfo, AuthenticationResult, EventType, InteractionStatus} from '@azure/msal-browser';
import {from, Observable} from 'rxjs';

import {environment} from '../../../environments/environment';
import {KeycloakAuthService} from './keycloak-auth.service';
import {HOME_ROUTE, loginRequest} from './msal-config';

/**
 * Point d'entrée unique de la session bénévole côté interface : qui est connecté,
 * comment se connecter, comment se déconnecter avec le fournisseur de l'environnement.
 *
 * Le compte est exposé en signal parce que le BackOffice tourne en détection de
 * changement « zoneless » : les évènements d'authentification arrivent hors de tout contexte
 * Angular, un champ simple ne redéclencherait aucun rendu.
 */
@Injectable({providedIn: 'root'})
export class AuthSessionService {
  private readonly msalService = inject(MsalService);
  private readonly msalBroadcastService = inject(MsalBroadcastService, {optional: true});
  private readonly keycloakAuthService = inject(KeycloakAuthService);

  private readonly _entraAccount = signal<AccountInfo | null>(null);
  readonly requiresAccountSelection = signal(false);
  readonly isKeycloak = environment.auth_provider === 'keycloak';

  /** Compte connecté du fournisseur configuré pour cet environnement. */
  readonly account = computed<AuthenticatedAccount | null>(() => this.isKeycloak
    ? this.keycloakAuthService.account()
    : this._entraAccount());

  readonly isAuthenticated = computed(() => this.account() !== null);

  /** Nom affichable : le nom du compte, à défaut la partie locale de l'adresse. */
  readonly displayName = computed(() => {
    const account = this.account();
    if (!account) {
      return '';
    }

    const username = account.username ?? account.email ?? '';
    return account.name?.trim() || username.split('@')[0];
  });

  /** Deux lettres au plus, pour la pastille de la barre de navigation. */
  readonly initials = computed(() => {
    const name = this.displayName();
    if (!name) {
      return '';
    }

    return name
      .split(/[\s.\-_]+/)
      .filter(part => part.length > 0)
      .slice(0, 2)
      .map(part => part[0].toUpperCase())
      .join('');
  });

  readonly email = computed(() => {
    const account = this.account();
    return account?.email || account?.username || '';
  });

  constructor() {
    if (this.isKeycloak) {
      return;
    }

    this.syncFromCache();
    if (!this.msalBroadcastService) {
      return;
    }

    // Une connexion aboutie n'émet pas forcément pendant qu'un composant écoute :
    // on suit à la fois les évènements et le retour à l'état « aucune interaction
    // en cours », qui suit systématiquement le traitement de la redirection.
    this.msalBroadcastService.msalSubject$.subscribe(message => {
      if (message.eventType === EventType.LOGIN_SUCCESS) {
        const account = (message.payload as AuthenticationResult | null)?.account ?? null;
        if (account) {
          this.msalService.instance.setActiveAccount(account);
        }
        this.syncFromCache(account ?? undefined);
      } else if (message.eventType === EventType.ACQUIRE_TOKEN_SUCCESS ||
        message.eventType === EventType.LOGOUT_SUCCESS) {
        this.syncFromCache();
      }
    });

    this.msalBroadcastService.inProgress$.subscribe(status => {
      if (status === InteractionStatus.None) {
        this.syncFromCache();
      }
    });
  }

  /**
   * Démarre la connexion Microsoft.
   *
   * `redirectStartPage` est indispensable : sans lui, MSAL mémorise la page d'où
   * part la connexion — l'écran de connexion — et y ramène l'utilisateur une fois
   * authentifié, qui se retrouve alors face au même écran sans jamais entrer dans
   * l'application.
   */
  login(startPage: string = HOME_ROUTE): Observable<void> {
    if (this.isKeycloak) {
      return from(this.keycloakAuthService.login(startPage));
    }

    const instance = this.msalService.instance;
    const requiresAccountSelection = !instance.getActiveAccount()
      && instance.getAllAccounts().length > 1;

    return this.msalService.loginRedirect({
      ...loginRequest,
      ...(requiresAccountSelection ? {prompt: 'select_account'} : {}),
      redirectStartPage: new URL(normalizeInternalRoute(startPage), window.location.origin).href,
    });
  }

  logout(): Observable<void> {
    if (this.isKeycloak) {
      return from(this.keycloakAuthService.logout());
    }

    return this.msalService.logoutRedirect({
      account: this._entraAccount() ?? undefined,
    });
  }

  consumeReturnUrl(): string | null {
    return this.isKeycloak ? this.keycloakAuthService.consumeReturnUrl() : null;
  }

  /**
   * Vide le cache du fournisseur courant puis recharge l'application.
   *
   * Filet de sécurité pour l'utilisateur : si une redirection a été interrompue
   * (onglet fermé, retour arrière), les données locales de session peuvent rester
   * incohérentes. Ce bouton les efface sans toucher aux données de l'association.
   */
  resetSession(): void {
    if (this.isKeycloak) {
      void this.keycloakAuthService.resetSession().finally(() => {
        window.location.replace(`${window.location.origin}/login`);
      });
      return;
    }

    void this.msalService.instance.clearCache().finally(() => {
      window.location.replace(window.location.origin);
    });
  }

  private syncFromCache(preferredAccount?: AccountInfo): void {
    const instance = this.msalService.instance;
    const activeAccount = instance.getActiveAccount();
    const cachedAccounts = instance.getAllAccounts();
    const active = activeAccount
      ?? preferredAccount
      ?? (cachedAccounts.length === 1 ? cachedAccounts[0] : null);

    if (active && !activeAccount) {
      instance.setActiveAccount(active);
    }

    this.requiresAccountSelection.set(!active && cachedAccounts.length > 1);
    this._entraAccount.set(active);
  }
}

export interface AuthenticatedAccount {
  name?: string | null;
  username?: string | null;
  email?: string | null;
}

function normalizeInternalRoute(value: string): string {
  const route = new URL(value, window.location.origin);
  if (route.origin !== window.location.origin || !value.startsWith('/') || value.startsWith('//')) {
    return HOME_ROUTE;
  }

  return `${route.pathname}${route.search}${route.hash}`;
}
