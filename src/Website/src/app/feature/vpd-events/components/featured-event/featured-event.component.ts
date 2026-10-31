import {CommonModule, DOCUMENT, isPlatformBrowser} from '@angular/common';
import {Component, effect, inject, Input, OnChanges, OnDestroy, PLATFORM_ID, signal, SimpleChanges} from '@angular/core';
import {RouterLink} from '@angular/router';
import {countdownLabel} from '../../../../shared/events/countdown';
import {eventIdentity, EventIdentity} from '../../../../shared/events/event-identity';
import {toIcs} from '../../../../shared/events/ics';
import {VpdEventEnum} from '../../../../shared/enums/vpdEvent.enum';
import {VpdEventModel} from '../../../../shared/models/vpdEvent.model';
import {HalloweenPreferences} from '../../../../shared/components/mascots/halloween-preferences';
import {VpdBouleBingoComponent} from '../../../../shared/components/mascots/boule-bingo.component';
import {VpdCitrouilleComponent} from '../../../../shared/components/mascots/vpd-citrouille.component';
import {VpdFantomeComponent} from '../../../../shared/components/mascots/vpd-fantome.component';
import {VpdPapillonComponent} from '../../../../shared/components/mascots/papillon.component';
import {TodayBannerHalloweenMotionService} from '../../../../core/layouts/today-banner/today-banner-halloween-motion.service';

@Component({
  selector: 'app-featured-event',
  standalone: true,
  imports: [CommonModule, RouterLink, VpdBouleBingoComponent, VpdCitrouilleComponent, VpdFantomeComponent, VpdPapillonComponent],
  templateUrl: './featured-event.component.html',
  styleUrl: './featured-event.component.scss',
})
export class FeaturedEventComponent implements OnChanges, OnDestroy {
  private readonly platformId = inject(PLATFORM_ID);
  private readonly document = inject(DOCUMENT);
  private readonly preferences = inject(HalloweenPreferences);
  private readonly motion = inject(TodayBannerHalloweenMotionService);
  private runnerTimer: ReturnType<typeof setTimeout> | undefined;

  @Input({required: true}) event!: VpdEventModel;

  protected readonly runnerActive = signal(false);
  protected readonly calm = this.preferences.calm;
  protected readonly visible = this.preferences.visible;

  constructor() {
    effect(() => {
      if (this.preferences.calm()) {
        this.runnerActive.set(false);
        clearTimeout(this.runnerTimer);
      }
    });
  }

  get identity(): EventIdentity {
    return eventIdentity(this.event);
  }

  get countdown(): string | null {
    return countdownLabel(this.event, new Date(), Number.POSITIVE_INFINITY);
  }

  get typeLabel(): string {
    switch (this.event.eventType) {
      case VpdEventEnum.Bingo:
        return 'Loto';
      case VpdEventEnum.Books:
        return 'Bourse aux livres';
      default:
        return 'Rendez-vous';
    }
  }

  get schedule(): string {
    const place = this.event.city;
    if (!this.event.hourOpenDoors) {
      return place;
    }
    const opening = `ouverture à ${this.parisTime(this.event.hourOpenDoors)}`;
    return place ? `${place} · ${opening}` : opening;
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['event'] && this.identity === 'halloween'
      && isPlatformBrowser(this.platformId) && !this.preferences.calm()
      && this.motion.claimEntrance()) {
      this.runnerActive.set(true);
      this.runnerTimer = setTimeout(() => this.runnerActive.set(false), 2800);
    }
  }

  ngOnDestroy(): void {
    clearTimeout(this.runnerTimer);
  }

  addToCalendar(): void {
    if (!isPlatformBrowser(this.platformId)) {
      return;
    }

    const url = new URL(`/evenement/${encodeURIComponent(this.event.id)}`, window.location.origin).href;
    const blob = new Blob([toIcs(this.event, url)], {type: 'text/calendar;charset=utf-8'});
    const objectUrl = URL.createObjectURL(blob);
    const anchor = this.document.createElement('a');
    anchor.href = objectUrl;
    anchor.download = `${this.fileSlug(this.event.name)}.ics`;
    this.document.body.appendChild(anchor);
    anchor.click();
    anchor.remove();
    setTimeout(() => URL.revokeObjectURL(objectUrl), 1000);
  }

  private parisTime(date: Date): string {
    const parts = new Intl.DateTimeFormat('fr-FR', {
      timeZone: 'UTC',
      hour: '2-digit',
      minute: '2-digit',
    }).formatToParts(date);
    const hour = parts.find(part => part.type === 'hour')?.value ?? '';
    const minute = parts.find(part => part.type === 'minute')?.value ?? '';
    return `${hour} h ${minute}`;
  }

  private fileSlug(name: string): string {
    return name.toLowerCase()
      .normalize('NFD')
      .replace(/[\u0300-\u036f]/g, '')
      .replace(/[^a-z0-9]+/g, '-')
      .replace(/^-|-$/g, '') || 'evenement';
  }
}
