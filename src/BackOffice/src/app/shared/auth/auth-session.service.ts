import {computed, inject, Injectable, signal} from '@angular/core';
import {MsalBroadcastService, MsalService} from '@azure/msal-angular';
import {AccountInfo, EventType, InteractionStatus} from '@azure/msal-browser';
import {Observable} from 'rxjs';

import {HOME_ROUTE, loginRequest} from './msal-config';

/**
 * Point d'entrée unique de la session bénévole côté interface : qui est connecté,
 * comment se connecter, comment se déconnecter.
 *
 * Le compte est exposé en signal parce que le BackOffice tourne en détection de
 * changement « zoneless » : les évènements MSAL arrivent hors de tout contexte
 * Angular, un champ simple ne redéclencherait aucun rendu.
 */
@Injectable({providedIn: 'root'})
export class AuthSessionService {
  private readonly msalService = inject(MsalService);
  private readonly msalBroadcastService = inject(MsalBroadcastService);

  private readonly _account = signal<AccountInfo | null>(null);

  /** Compte Entra actif, ou `null` tant que personne n'est connecté. */
  readonly account = this._account.asReadonly();

  readonly isAuthenticated = computed(() => this._account() !== null);

  /** Nom affichable : le nom du compte, à défaut la partie locale de l'adresse. */
  readonly displayName = computed(() => {
    const account = this._account();
    if (!account) {
      return '';
    }

    return account.name?.trim() || account.username.split('@')[0];
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

  readonly email = computed(() => this._account()?.username ?? '');

  constructor() {
    this.syncFromCache();

    // Une connexion aboutie n'émet pas forcément pendant qu'un composant écoute :
    // on suit à la fois les évènements et le retour à l'état « aucune interaction
    // en cours », qui suit systématiquement le traitement de la redirection.
    this.msalBroadcastService.msalSubject$.subscribe(message => {
      if (message.eventType === EventType.LOGIN_SUCCESS ||
        message.eventType === EventType.ACQUIRE_TOKEN_SUCCESS ||
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
    return this.msalService.loginRedirect({
      ...loginRequest,
      redirectStartPage: new URL(startPage, window.location.origin).href,
    });
  }

  logout(): Observable<void> {
    return this.msalService.logoutRedirect({
      account: this._account() ?? undefined,
    });
  }

  /**
   * Vide le cache MSAL local puis recharge l'application.
   *
   * Filet de sécurité pour l'utilisateur : si une redirection a été interrompue
   * (onglet fermé, retour arrière), MSAL garde un marqueur « interaction en
   * cours » qui fait échouer toutes les tentatives suivantes. Sans ce bouton, la
   * seule issue est de vider le stockage du navigateur à la main.
   */
  resetSession(): void {
    void this.msalService.instance.clearCache().finally(() => {
      window.location.replace(window.location.origin);
    });
  }

  private syncFromCache(): void {
    const instance = this.msalService.instance;
    const active = instance.getActiveAccount() ?? instance.getAllAccounts()[0] ?? null;

    if (active && !instance.getActiveAccount()) {
      instance.setActiveAccount(active);
    }

    this._account.set(active);
  }
}
