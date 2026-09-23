import { isPlatformBrowser } from '@angular/common';
import {
  afterNextRender,
  Component,
  effect,
  ElementRef,
  HostListener,
  inject,
  Injector,
  input,
  PLATFORM_ID,
  Renderer2,
  signal,
  ViewChild,
} from '@angular/core';
import { SiteNavItem } from '../nav-items';

@Component({
  selector: 'app-navigation-mobile',
  templateUrl: './navigation-mobile.component.html',
  standalone: false,
})
export class NavigationMobileComponent {
  private readonly platformId = inject(PLATFORM_ID);
  private readonly injector = inject(Injector);

  @ViewChild('menuToggle') private menuToggle?: ElementRef<HTMLButtonElement>;
  @ViewChild('menuCloseButton') private menuCloseButton?: ElementRef<HTMLButtonElement>;
  @ViewChild('menuPanel') private menuPanel?: ElementRef<HTMLElement>;

  navItems = input.required<SiteNavItem[]>();
  activeUrl = input<string | null>(null);
  currentUrl = input<string>('');
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
    if (this.isOpen()) {
      this.close(true);
      return;
    }

    this.isOpen.set(true);
    afterNextRender(() => this.menuCloseButton?.nativeElement.focus(), { injector: this.injector });
  }

  close(restoreFocus = true): void {
    if (!this.isOpen()) return;

    this.isOpen.set(false);

    if (restoreFocus) {
      afterNextRender(() => this.menuToggle?.nativeElement.focus(), { injector: this.injector });
    }
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (this.isOpen()) this.close(true);
  }

  onPanelKeydown(event: KeyboardEvent): void {
    if (event.key !== 'Tab' || !this.menuPanel) return;

    const panel = this.menuPanel.nativeElement;
    const focusable = Array.from(panel.querySelectorAll<HTMLElement>(
      'a[href], button:not(:disabled), [tabindex]:not([tabindex="-1"])',
    ));
    if (!focusable.length) return;

    const first = focusable[0];
    const last = focusable[focusable.length - 1];
    const activeElement = globalThis.document.activeElement;

    if (event.shiftKey && (activeElement === first || !panel.contains(activeElement))) {
      event.preventDefault();
      last.focus();
    } else if (!event.shiftKey && (activeElement === last || !panel.contains(activeElement))) {
      event.preventDefault();
      first.focus();
    }
  }

  /** Surligne la sous-rubrique courante dans la liste dépliée sous son parent. */
  isChildActive(child: SiteNavItem): boolean {
    return this.currentUrl().startsWith(child.matchPrefix ?? child.url);
  }
}
