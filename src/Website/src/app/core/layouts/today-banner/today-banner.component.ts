import {isPlatformBrowser} from '@angular/common';
import {Component, computed, inject, OnInit, PLATFORM_ID, signal} from '@angular/core';
import {TodayEventService} from '../../../shared/services/today-event.service';
import {
  isTodayBannerDismissed,
  TODAY_BANNER_STORAGE_KEY,
  TodayBannerDismissal,
  todayBannerLabel,
  todayBannerSchedule,
  todayBannerStage,
} from './today-banner-state';

/** E4 · Bandeau du prochain rendez-vous, au-dessus de l'en-tête, sur toutes les pages sauf le tableau. */
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
  protected readonly halloween = computed(() => this.today()?.liveTheme === 'Halloween');
  protected readonly stage = computed(() => {
    const today = this.today();
    return today === null ? null : todayBannerStage(today, new Date());
  });
  protected readonly label = computed(() => {
    const today = this.today();
    const stage = this.stage();
    return today === null || stage === null ? '' : todayBannerLabel(today, stage, new Date());
  });
  protected readonly schedule = computed(() => {
    const today = this.today();
    const stage = this.stage();
    return today === null || stage === null ? '' : todayBannerSchedule(today, stage, new Date());
  });
  protected readonly visible = computed(() => {
    const today = this.today();
    return today !== null && !isTodayBannerDismissed(today, this.dismissal(), new Date());
  });

  ngOnInit(): void {
    this.dismissal.set(this.readDismissal());
    void this.todayEvent.start();
  }

  protected close(): void {
    const today = this.today();
    const stage = this.stage();
    if (today === null || stage === null) {
      return;
    }
    const dismissal: TodayBannerDismissal = {eventId: today.id, stage};
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
