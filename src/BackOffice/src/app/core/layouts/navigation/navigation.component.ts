import {Component, DestroyRef, inject, signal} from '@angular/core';
import {takeUntilDestroyed} from '@angular/core/rxjs-interop';
import {NavigationEnd, Router} from '@angular/router';
import {filter} from 'rxjs/operators';

import {NavigationItemInterface} from '../../../shared/interfaces/navigationItem.interface';

@Component({
  selector: 'app-navigation',
  templateUrl: './navigation.component.html',
  standalone: false,
})
export class NavigationComponent {
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  /**
   * URL courante, suivie en signal : la détection de changement du BackOffice est
   * « zoneless », lire `router.url` directement dans le gabarit ne redéclencherait
   * aucun rendu à la navigation et la rubrique active resterait figée.
   */
  private readonly url = signal(this.router.url);

  readonly navigationUrls: NavigationItemInterface[] = [
    {
      url: "/actualites",
      title: "L'actualité",
      subNav: []
    },
    {
      url: "/evenements",
      title: "Les événements",
      subNav: []
    },
    {
      url: "/caisse",
      title: "La Caisse",
      subNav: []
    },
    {
      url: "/administration",
      title: "Catalogue",
      subNav: []
    },
  ];

  constructor() {
    this.router.events
      .pipe(
        filter((event): event is NavigationEnd => event instanceof NavigationEnd),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe(event => this.url.set(event.urlAfterRedirects));
  }

  protected isActive(item: NavigationItemInterface): boolean {
    return this.url().startsWith(item.url);
  }
}
