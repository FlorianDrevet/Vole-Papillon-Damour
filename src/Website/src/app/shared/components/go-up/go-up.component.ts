import {isPlatformBrowser} from '@angular/common';
import {Component, HostListener, inject, OnInit, PLATFORM_ID, signal} from '@angular/core';

@Component({
  selector: 'app-go-up',
  templateUrl: './go-up.component.html',
  styleUrl: './go-up.component.scss',
  standalone: false
})
export class GoUpComponent implements OnInit {
  private readonly platformId = inject(PLATFORM_ID);

  readonly isVisible = signal(false);

  ngOnInit(): void {
    this.updateVisibility();
  }

  @HostListener('window:scroll')
  onWindowScroll(): void {
    this.updateVisibility();
  }

  scrollToTop(): void {
    if (!isPlatformBrowser(this.platformId)) {
      return;
    }

    const behavior = globalThis.matchMedia('(prefers-reduced-motion: reduce)').matches ? 'auto' : 'smooth';
    globalThis.scrollTo({ top: 0, behavior });
  }

  private updateVisibility(): void {
    if (!isPlatformBrowser(this.platformId)) return;

    const shouldShow = globalThis.scrollY > 480;
    if (shouldShow !== this.isVisible()) this.isVisible.set(shouldShow);
  }
}
