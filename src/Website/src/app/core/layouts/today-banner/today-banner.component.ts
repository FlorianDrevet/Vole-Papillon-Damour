import {isPlatformBrowser} from '@angular/common';
import {Component, computed, DestroyRef, inject, OnInit, PLATFORM_ID, signal} from '@angular/core';
import {TodayEventService} from '../../../shared/services/today-event.service';
import {
  HALLOWEEN_PHOTOS_DISMISSED_STORAGE_KEY,
  halloweenPhotosAnnouncementRemaining,
  isHalloweenPhotosAnnouncementActive,
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
  private readonly destroyRef = inject(DestroyRef);
  private readonly platformId = inject(PLATFORM_ID);
  private readonly dismissal = signal<TodayBannerDismissal | null>(null);
  private readonly photosDismissed = signal(false);
  private readonly photoAnnouncementClock = signal(0);
  private photoAnnouncementTimer: ReturnType<typeof setTimeout> | undefined;

  protected readonly today = this.todayEvent.today;
  protected readonly photoAnnouncement = computed(() => {
    this.today();
    this.photoAnnouncementClock();
    return isHalloweenPhotosAnnouncementActive(new Date())
      && !this.photosDismissed();
  });
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
    if (this.photoAnnouncement()) {
      return true;
    }
    const today = this.today();
    return today !== null && !isTodayBannerDismissed(today, this.dismissal(), new Date());
  });

  constructor() {
    this.destroyRef.onDestroy(() => clearTimeout(this.photoAnnouncementTimer));
  }

  ngOnInit(): void {
    this.dismissal.set(this.readDismissal());
    this.photosDismissed.set(this.readPhotoDismissal());
    const now = new Date();
    if (isPlatformBrowser(this.platformId) && isHalloweenPhotosAnnouncementActive(now)) {
      this.photoAnnouncementTimer = setTimeout(
        () => this.photoAnnouncementClock.update(value => value + 1),
        halloweenPhotosAnnouncementRemaining(now),
      );
    }
    void this.todayEvent.start();
  }

  protected close(): void {
    const today = this.today();
    const stage = this.stage();
    if (today === null || stage === null) {
      return;
    }
    const dismissal: TodayBannerDismissal = {eventId: today.id, stage};
    this.saveDismissal(dismissal);
  }

  protected closePhotoAnnouncement(): void {
    this.photosDismissed.set(true);
    try {
      localStorage.setItem(HALLOWEEN_PHOTOS_DISMISSED_STORAGE_KEY, 'true');
    } catch {
      // Stockage indisponible : le bandeau reste fermé jusqu'au rechargement.
    }
  }

  private saveDismissal(dismissal: TodayBannerDismissal): void {
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

  private readPhotoDismissal(): boolean {
    if (!isPlatformBrowser(this.platformId)) {
      return false;
    }
    try {
      return localStorage.getItem(HALLOWEEN_PHOTOS_DISMISSED_STORAGE_KEY) === 'true';
    } catch {
      return false;
    }
  }
}
