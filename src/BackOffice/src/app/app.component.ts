import {Component, DestroyRef, inject, signal} from '@angular/core';
import {takeUntilDestroyed} from '@angular/core/rxjs-interop';
import {ActivatedRoute, NavigationEnd, Router} from '@angular/router';
import {filter} from 'rxjs/operators';

import {AuthSessionService} from './shared/auth/auth-session.service';

@Component({
    selector: 'app-root',
    templateUrl: './app.component.html',
    standalone: false
})
export class AppComponent {
  private readonly router = inject(Router);
  private readonly activatedRoute = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);

  // Instancié dès le démarrage : c'est lui qui adopte le compte déjà en cache
  // comme compte actif et qui suit les évènements MSAL pour toute l'application.
  private readonly authSession = inject(AuthSessionService);

  /**
   * Certaines pages occupent tout l'écran et n'ont ni barre de navigation ni pied
   * de page : la connexion, l'atterrissage après redirection, et le tableau du
   * loto affiché en salle. Chaque route le déclare (`data.chrome`) plutôt que de
   * laisser la navigation et le pied de page tester l'URL chacun de leur côté —
   * la règle était dupliquée et divergeait déjà entre les deux.
   */
  protected readonly showChrome = signal(this.resolveChrome());

  constructor() {
    this.router.events
      .pipe(
        filter((event): event is NavigationEnd => event instanceof NavigationEnd),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe(() => this.showChrome.set(this.resolveChrome()));
  }

  private resolveChrome(): boolean {
    let route = this.activatedRoute;
    while (route.firstChild) {
      route = route.firstChild;
    }

    return route.snapshot.data['chrome'] !== false;
  }
}
