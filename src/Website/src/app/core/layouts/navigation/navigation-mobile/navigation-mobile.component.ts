import { isPlatformBrowser } from '@angular/common';
import { Component, effect, inject, input, PLATFORM_ID, Renderer2, signal } from '@angular/core';
import { SiteNavItem } from '../nav-items';

@Component({
  selector: 'app-navigation-mobile',
  templateUrl: './navigation-mobile.component.html',
  standalone: false,
})
export class NavigationMobileComponent {
  private readonly platformId = inject(PLATFORM_ID);

  navItems = input.required<SiteNavItem[]>();
  activeUrl = input<string | null>(null);
  crumb = input<string>('Accueil');

  isOpen = signal(false);

  constructor(private readonly renderer: Renderer2) {
    effect(() => {
      if (!isPlatformBrowser(this.platformId)) return;
      if (this.isOpen()) {
        this.renderer.addClass(globalThis.document.body, 'no-scroll');
      } else {
        this.renderer.removeClass(globalThis.document.body, 'no-scroll');
      }
    });
  }

  toggle(): void {
    this.isOpen.set(!this.isOpen());
  }

  close(): void {
    this.isOpen.set(false);
  }
}
