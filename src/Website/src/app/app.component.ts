import {DOCUMENT} from '@angular/common';
import {Component, Inject, OnDestroy, OnInit, signal} from '@angular/core';
import {NavigationCancel, NavigationEnd, NavigationError, NavigationStart, Router} from '@angular/router';
import {Subject, filter, takeUntil} from 'rxjs';
import {WEBSITE_ORIGIN} from '../website-origin';

@Component({
    selector: 'app-root',
    templateUrl: './app.component.html',
    styleUrl: './app.component.scss',
    standalone: false
})
export class AppComponent implements OnInit, OnDestroy {
  title = "Vole Papillon d'Amour";
  readonly isNavigating = signal(false);

  private readonly destroyed = new Subject<void>();

  constructor(
    private readonly router: Router,
    @Inject(DOCUMENT) private readonly document: Document,
  ) {}

  ngOnInit(): void {
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
        this.isNavigating.set(event instanceof NavigationStart);
        if (event instanceof NavigationEnd) {
          this.updateCanonical(event.urlAfterRedirects);
        }
      });

    if (this.router.navigated) {
      this.updateCanonical(this.router.url);
    }
  }

  ngOnDestroy(): void {
    this.destroyed.next();
    this.destroyed.complete();
  }

  private updateCanonical(url: string): void {
    const path = url.split(/[?#]/, 1)[0].replace(/\/+$/, '') || '/accueil';
    let canonical = this.document.head.querySelector<HTMLLinkElement>('link[rel="canonical"]');

    if (!canonical) {
      canonical = this.document.createElement('link');
      canonical.rel = 'canonical';
      this.document.head.appendChild(canonical);
    }

    canonical.href = `${WEBSITE_ORIGIN}${path}`;
  }
}
