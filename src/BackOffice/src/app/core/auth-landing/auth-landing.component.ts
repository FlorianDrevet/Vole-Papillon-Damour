import {Component, DestroyRef, inject, OnInit, signal} from '@angular/core';
import {takeUntilDestroyed} from '@angular/core/rxjs-interop';
import {Router} from '@angular/router';
import {MsalBroadcastService} from '@azure/msal-angular';
import {InteractionStatus} from '@azure/msal-browser';
import {filter, take, timer} from 'rxjs';

import {AuthSessionService} from '../../shared/auth/auth-session.service';
import {HOME_ROUTE, LOGIN_ROUTE} from '../../shared/auth/msal-config';

/** Au-delà de ce délai, la redirection est considérée comme bloquée. */
const REDIRECT_TIMEOUT_MS = 12_000;

/**
 * Page d'atterrissage de la racine `/`.
 *
 * C'est l'URL sur laquelle Entra renvoie après authentification. Elle ne porte
 * volontairement aucun MsalGuard : quand le guard était appliqué ici, il traitait
 * la redirection en même temps que `MsalRedirectComponent` et, le temps que MSAL
 * fasse repartir le navigateur vers la page demandée, la navigation Angular
 * restait suspendue — l'application affichait un cadre vide, sans aucun moyen
 * d'en sortir.
 *
 * Ici, on se contente d'attendre que MSAL ait fini, puis d'aiguiller. Et si rien
 * n'arrive, on le dit au lieu de laisser tourner un chargement sans fin.
 */
@Component({
  selector: 'app-auth-landing',
  templateUrl: './auth-landing.component.html',
  standalone: false,
})
export class AuthLandingComponent implements OnInit {
  private readonly msalBroadcastService = inject(MsalBroadcastService);
  private readonly authSession = inject(AuthSessionService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly isStalled = signal(false);

  ngOnInit(): void {
    this.msalBroadcastService.inProgress$
      .pipe(
        filter(status => status === InteractionStatus.None),
        take(1),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe(() => this.routeOnward());

    timer(REDIRECT_TIMEOUT_MS)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.isStalled.set(true));
  }

  protected resetSession(): void {
    this.authSession.resetSession();
  }

  private routeOnward(): void {
    void this.router.navigateByUrl(
      this.authSession.isAuthenticated() ? HOME_ROUTE : LOGIN_ROUTE,
    );
  }
}
