import {isPlatformBrowser} from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  Inject,
  OnDestroy,
  OnInit,
  PLATFORM_ID,
} from '@angular/core';
import {Meta} from '@angular/platform-browser';
import {NavigationEnd, Router} from '@angular/router';
import {Subject, filter, takeUntil} from 'rxjs';

import {CatalogAuthService} from './core/catalog-auth.service';
import {catalogRobotsForUrl} from './core/catalog-robots';

@Component({
  selector: 'app-root',
  standalone: false,
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AppComponent implements OnInit, OnDestroy {
  private readonly destroyed = new Subject<void>();

  constructor(
    private readonly router: Router,
    private readonly meta: Meta,
    private readonly auth: CatalogAuthService,
    @Inject(PLATFORM_ID) private readonly platformId: object,
  ) {}

  ngOnInit(): void {
    this.updateRobotsMetadata(this.router.url);
    this.initializeAuthenticationRedirect();
    this.router.events
      .pipe(
        filter((event): event is NavigationEnd => event instanceof NavigationEnd),
        takeUntil(this.destroyed),
      )
      .subscribe(event => this.updateRobotsMetadata(event.urlAfterRedirects));
  }

  ngOnDestroy(): void {
    this.destroyed.next();
    this.destroyed.complete();
  }

  private updateRobotsMetadata(url: string): void {
    this.meta.updateTag({name: 'robots', content: catalogRobotsForUrl(url)});
  }

  private initializeAuthenticationRedirect(): void {
    if (isPlatformBrowser(this.platformId) && hasAuthenticationResponse(window.location)) {
      void this.auth.initialize();
    }
  }
}

function hasAuthenticationResponse(location: Pick<Location, 'hash' | 'search'>): boolean {
  return /(?:[?#&])(code|error)=/i.test(`${location.search}${location.hash}`);
}
