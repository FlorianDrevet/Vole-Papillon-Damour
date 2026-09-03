import {Component, computed, inject, signal} from '@angular/core';
import {ActivatedRoute} from '@angular/router';

import {environment} from '../../../environments/environment';
import {AuthSessionService} from '../../shared/auth/auth-session.service';
import {HOME_ROUTE} from '../../shared/auth/msal-config';

/**
 * Écran de connexion.
 *
 * Il gère aussi le cas « déjà connecté » : l'application y renvoie quand un appel
 * à l'API est refusé, et l'ancienne version n'affichait alors qu'un bouton
 * « Se connecter » qui relançait une redirection pour revenir au même endroit.
 * Le bénévole voit maintenant sous quel compte il est, peut entrer directement,
 * ou changer de compte.
 */
@Component({
  selector: 'app-login',
  templateUrl: './login.component.html',
  standalone: false,
})
export class LoginComponent {
  private readonly authSession = inject(AuthSessionService);
  private readonly route = inject(ActivatedRoute);

  protected readonly homeRoute = HOME_ROUTE;
  protected readonly websiteUrl = environment.url_vpd_web_site;
  protected readonly isAuthenticated = this.authSession.isAuthenticated;
  protected readonly displayName = this.authSession.displayName;
  protected readonly email = this.authSession.email;
  protected readonly initials = this.authSession.initials;

  protected readonly hasFailed = signal(false);

  /** Renseigné par l'intercepteur HTTP quand une session n'est plus acceptée. */
  protected readonly wasSessionRejected = computed(
    () => this.route.snapshot.queryParamMap.get('raison') === 'session',
  );

  protected onLoginClick(): void {
    this.hasFailed.set(false);

    // `loginRedirect` échoue quand MSAL considère qu'une interaction est déjà en
    // cours — typiquement après une redirection interrompue. L'ancienne version
    // affichait une notification qui disparaissait toute seule ; le message reste
    // maintenant affiché et renvoie vers la seule action qui débloque vraiment.
    this.authSession.login().subscribe({
      error: () => this.hasFailed.set(true),
    });
  }

  protected onLogoutClick(): void {
    this.authSession.logout().subscribe({
      error: () => this.hasFailed.set(true),
    });
  }

  protected resetSession(): void {
    this.authSession.resetSession();
  }
}
