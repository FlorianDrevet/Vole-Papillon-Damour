import { Component, OnInit, signal } from '@angular/core';
import { Router } from '@angular/router';
import { SITE_NAV_ITEMS, getActiveNavUrl, getBreadcrumb, SiteNavItem } from './nav-items';

@Component({
  selector: 'app-navigation',
  templateUrl: './navigation.component.html',
  standalone: false,
})
export class NavigationComponent implements OnInit {
  readonly navItems: SiteNavItem[] = SITE_NAV_ITEMS;

  url = signal<string>('');
  activeUrl = signal<string | null>(null);
  crumb = signal<string>('Accueil');

  constructor(private readonly router: Router) {}

  ngOnInit(): void {
    this.url.set(this.router.url);
    this.activeUrl.set(getActiveNavUrl(this.router.url));
    this.crumb.set(getBreadcrumb(this.router.url));

    this.router.events.subscribe(() => {
      this.url.set(this.router.url);
      this.activeUrl.set(getActiveNavUrl(this.router.url));
      this.crumb.set(getBreadcrumb(this.router.url));
    });
  }

  /** Surligne la sous-rubrique courante dans le sous-menu déplié. */
  isChildActive(childUrl: string): boolean {
    return this.url().startsWith(childUrl);
  }
}
