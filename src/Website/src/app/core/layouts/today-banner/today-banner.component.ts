import {isPlatformBrowser} from '@angular/common';
import {Component, computed, inject, OnInit, PLATFORM_ID, signal} from '@angular/core';
import {TodayEventService} from '../../../shared/services/today-event.service';
import {
  isTodayBannerDismissed,
  parisDay,
  TODAY_BANNER_STORAGE_KEY,
  TodayBannerDismissal,
  todayBannerSchedule,
  todayBannerVariant,
} from './today-banner-state';

/** E5 · Bandeau « Aujourd'hui », au-dessus de l'en-tête, sur toutes les pages sauf le tableau. */
@Component({
  selector: 'app-today-banner',
  templateUrl: './today-banner.component.html',
  standalone: false
})
export class TodayBannerComponent implements OnInit {
  private readonly todayEvent = inject(TodayEventService);
  private readonly platformId = inject(PLATFORM_ID);
  private readonly dismissal = signal<TodayBannerDismissal | null>(null);

  protected readonly today = this.todayEvent.today;
  protected readonly variant = computed(() => {
    const today = this.today();
    return today === null ? null : todayBannerVariant(today);
  });
  protected readonly schedule = computed(() => {
    const today = this.today();
    return today === null ? '' : todayBannerSchedule(today);
  });
  protected readonly visible = computed(() => {
    const today = this.today();
    return today !== null && !isTodayBannerDismissed(today, this.dismissal(), parisDay(new Date()));
  });

  ngOnInit(): void {
    this.dismissal.set(this.readDismissal());
    void this.todayEvent.start();
  }

  protected close(): void {
    const today = this.today();
    const variant = this.variant();
    if (today === null || variant === null) {
      return;
    }
    const dismissal: TodayBannerDismissal = {eventId: today.id, day: parisDay(new Date()), variant};
    this.dismissal.set(dismissal);
    try {
      localStorage.setItem(TODAY_BANNER_STORAGE_KEY, JSON.stringify(dismissal));
    } catch {
      // Stockage indisponible : le bandeau reste fermé jusqu'au rechargement.
    }
  }

  private readDismissal(): TodayBannerDismissal | null {
    if (!isPlatformBrowser(this.platformId)) {
      return null;
    }
    try {
      const stored = localStorage.getItem(TODAY_BANNER_STORAGE_KEY);
      return stored === null ? null : JSON.parse(stored) as TodayBannerDismissal;
    } catch {
      return null;
    }
  }
}
