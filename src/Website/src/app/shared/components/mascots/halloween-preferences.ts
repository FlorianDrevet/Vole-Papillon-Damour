import {computed, DestroyRef, inject, Injectable, PLATFORM_ID, signal, WritableSignal} from '@angular/core';
import {isPlatformBrowser} from '@angular/common';

const CALM_STORAGE_KEY = 'vpd.tableau.calm';

@Injectable({providedIn: 'root'})
export class HalloweenPreferences {
  private readonly platformId = inject(PLATFORM_ID);
  private readonly destroyRef = inject(DestroyRef);
  private readonly savedCalm = signal(false);

  readonly phone = signal(false);
  readonly reducedMotion = signal(false);
  readonly calm = computed(() => this.reducedMotion() || this.savedCalm());
  readonly visible = signal(false);
  readonly visibleSince = signal(0);

  constructor() {
    if (!isPlatformBrowser(this.platformId)) {
      return;
    }

    this.readCalmPreference();
    this.watchMedia('(max-width: 767.98px)', this.phone);
    this.watchMedia('(prefers-reduced-motion: reduce)', this.reducedMotion);

    const updateVisibility = () => {
      const visible = document.visibilityState === 'visible';
      this.visible.set(visible);
      if (visible) {
        this.visibleSince.set(Date.now());
      }
    };

    updateVisibility();
    document.addEventListener('visibilitychange', updateVisibility);
    this.destroyRef.onDestroy(() => document.removeEventListener('visibilitychange', updateVisibility));
  }

  toggleCalm(): void {
    const enabled = !this.savedCalm();
    this.savedCalm.set(enabled);
    try {
      localStorage.setItem(CALM_STORAGE_KEY, String(enabled));
    } catch {
      // Storage can be unavailable in private or restricted browsing contexts.
    }
  }

  private readCalmPreference(): void {
    try {
      this.savedCalm.set(localStorage.getItem(CALM_STORAGE_KEY) === 'true');
    } catch {
      // The in-memory default remains usable when storage is unavailable.
    }
  }

  private watchMedia(query: string, target: WritableSignal<boolean>): void {
    const media = window.matchMedia(query);
    target.set(media.matches);

    const update = (event: MediaQueryListEvent) => target.set(event.matches);
    media.addEventListener('change', update);
    this.destroyRef.onDestroy(() => media.removeEventListener('change', update));
  }
}
