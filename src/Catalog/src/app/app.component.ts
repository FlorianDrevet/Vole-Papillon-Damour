import {isPlatformBrowser} from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  Inject,
  OnDestroy,
  OnInit,
  PLATFORM_ID,
  signal,
} from '@angular/core';
import {Meta} from '@angular/platform-browser';
import {NavigationCancel, NavigationEnd, NavigationError, NavigationStart, Router} from '@angular/router';
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
  private hasStartedNavigation = false;
  readonly isAdministrationRoute = signal(false);
  readonly isNavigating = signal(false);

  constructor(
    private readonly router: Router,
    private readonly meta: Meta,
    private readonly auth: CatalogAuthService,
    @Inject(PLATFORM_ID) private readonly platformId: object,
  ) {}

  ngOnInit(): void {
    this.updateRobotsMetadata(this.router.url);
    this.isAdministrationRoute.set(this.isAdminUrl(this.router.url));
    this.initializeAuthentication();
    this.router.events
      .pipe(
        filter(event =>
          event instanceof NavigationStart ||
          event instanceof NavigationEnd ||
          event instanceof NavigationCancel ||
          event instanceof NavigationError,
        ),
        takeUntil(this.destroyed),
      )
      .subscribe(event => {
        if (event instanceof NavigationStart) {
          this.isNavigating.set(
            !this.hasStartedNavigation || this.isPageTransition(this.router.url, event.url),
          );
          this.hasStartedNavigation = true;
        } else {
          this.isNavigating.set(false);
        }
        if (!(event instanceof NavigationEnd)) {
          return;
        }

        this.updateRobotsMetadata(event.urlAfterRedirects);
        this.isAdministrationRoute.set(this.isAdminUrl(event.urlAfterRedirects));
      });
  }

  ngOnDestroy(): void {
    this.destroyed.next();
    this.destroyed.complete();
  }

  private updateRobotsMetadata(url: string): void {
    this.meta.updateTag({name: 'robots', content: catalogRobotsForUrl(url)});
  }

  private initializeAuthentication(): void {
    if (isPlatformBrowser(this.platformId)) {
      void this.auth.initialize().catch(() => undefined);
    }
  }

  private isPageTransition(currentUrl: string, targetUrl: string): boolean {
    return this.urlPath(currentUrl) !== this.urlPath(targetUrl);
  }

  private urlPath(url: string): string {
    return url.split(/[?#]/, 1)[0].replace(/\/$/, '') || '/';
  }

  private isAdminUrl(url: string): boolean {
    return url.split(/[?#]/, 1)[0].replace(/\/$/, '') === '/administration';
  }
}
