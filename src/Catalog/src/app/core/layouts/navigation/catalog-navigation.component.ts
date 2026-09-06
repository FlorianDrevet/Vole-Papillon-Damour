import {isPlatformBrowser} from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  PLATFORM_ID,
  Renderer2,
  computed,
  effect,
  inject,
  signal,
} from '@angular/core';
import {NavigationEnd, Router} from '@angular/router';
import type {AccountInfo} from '@azure/msal-browser';
import {Subject, filter, takeUntil} from 'rxjs';

import {CatalogAuthService} from '../../catalog-auth.service';
import {CATALOG_NAV_ITEMS, CatalogNavItem} from './catalog-nav-items';

@Component({
  selector: 'app-catalog-navigation',
  standalone: false,
  templateUrl: './catalog-navigation.component.html',
  styleUrls: ['./catalog-navigation.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CatalogNavigationComponent {
  private readonly auth = inject(CatalogAuthService);

  readonly navItems = CATALOG_NAV_ITEMS;
  readonly url = signal('');
  readonly menuOpen = signal(false);
  readonly accountMenuOpen = signal(false);
  readonly account = this.auth.account;
  readonly isAuthenticated = this.auth.isAuthenticated;
  readonly isAdministration = computed(() => this.url().split(/[?#]/, 1)[0].startsWith('/administration'));
  readonly accountTriggerLabel = computed(() => {
    if (!this.isAdministration()) {
      return 'Mon compte';
    }

    const account = this.account();
    return account ? this.initials(account) : 'Administration';
  });
  readonly accountName = computed(() => this.displayAccount(this.account()));
  readonly accountInitials = computed(() => {
    const account = this.account();
    return account ? this.initials(account) : 'MO';
  });

  private readonly platformId = inject(PLATFORM_ID);
  private readonly renderer = inject(Renderer2);
  private readonly router = inject(Router);
  private readonly destroyed = new Subject<void>();
  private readonly suppressedGenreMenu = signal(false);

  constructor() {
    effect(() => {
      if (!isPlatformBrowser(this.platformId)) {
        return;
      }

      if (this.menuOpen()) {
        this.renderer.addClass(document.body, 'no-scroll');
      } else {
        this.renderer.removeClass(document.body, 'no-scroll');
      }
    });

    this.url.set(this.router.url);
    this.router.events
      .pipe(
        filter((event): event is NavigationEnd => event instanceof NavigationEnd),
        takeUntil(this.destroyed),
      )
      .subscribe(event => {
        this.url.set(event.urlAfterRedirects);
        this.closeMenus();
      });
  }

  ngOnDestroy(): void {
    this.destroyed.next();
    this.destroyed.complete();
  }

  isActive(item: CatalogNavItem): boolean {
    if (item.url === '/') {
      return this.url().split(/[?#]/, 1)[0] === '/';
    }

    return this.url().split(/[?#]/, 1)[0].startsWith(item.url);
  }

  isChildActive(item: CatalogNavItem): boolean {
    const currentUrl = this.url();
    if (item.queryParams?.['genre']) {
      return currentUrl.includes(`genre=${encodeURIComponent(item.queryParams['genre'])}`)
        || currentUrl.includes(`genre=${item.queryParams['genre']}`);
    }

    return this.isActive(item);
  }

  isGenreMenuSuppressed(): boolean {
    return this.suppressedGenreMenu();
  }

  suppressGenreMenu(): void {
    this.suppressedGenreMenu.set(true);
  }

  releaseGenreMenu(): void {
    this.suppressedGenreMenu.set(false);
  }

  toggleAccountMenu(event: Event): void {
    event.preventDefault();
    this.accountMenuOpen.update(open => !open);
  }

  closeMenus(): void {
    this.menuOpen.set(false);
    this.accountMenuOpen.set(false);
  }

  closeMobileMenu(): void {
    this.menuOpen.set(false);
  }

  toggleMobileMenu(): void {
    this.menuOpen.update(open => !open);
    this.accountMenuOpen.set(false);
  }

  async logout(): Promise<void> {
    this.closeMenus();
    try {
      await this.auth.logout();
    } catch {
      // The account page owns error feedback; the shell only closes its menu.
    }
  }

  private displayAccount(account: AccountInfo | null): string {
    return account?.name?.trim() || account?.username || '';
  }

  private initials(account: AccountInfo): string {
    const source = account.name?.trim() || account.username.split('@')[0];
    const words = source.split(/[\s._-]+/).filter(Boolean);
    return words.slice(0, 2).map(word => word[0]).join('').toUpperCase().padEnd(2, '•');
  }
}
