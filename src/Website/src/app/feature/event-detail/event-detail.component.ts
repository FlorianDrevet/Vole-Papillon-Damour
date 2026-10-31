import {isPlatformBrowser} from '@angular/common';
import {Component, computed, effect, inject, OnDestroy, OnInit, PendingTasks, PLATFORM_ID, signal} from '@angular/core';
import {DomSanitizer, SafeResourceUrl} from '@angular/platform-browser';
import {ActivatedRoute} from "@angular/router";
import {VpdEventsFacadeService} from "../../shared/facades/vpd-events.facade.service";
import {TodayEventService} from "../../shared/services/today-event.service";
import {VpdEventModel} from "../../shared/models/vpdEvent.model";
import {VpdEventEnum} from "../../shared/enums/vpdEvent.enum";
import {eventMapsEmbedUrl, eventMapsUrl, formatEventAddress} from "../../shared/utils/event-address.util";
import {eventIdentity, EventIdentity} from '../../shared/events/event-identity';
import {countdownLabel, parisDay} from '../../shared/events/countdown';
import {trackSsrDataRequest} from '../../shared/events/ssr-pending-task';
import {HalloweenPreferences} from '../../shared/components/mascots/halloween-preferences';
import {TodayBannerHalloweenMotionService} from '../../core/layouts/today-banner/today-banner-halloween-motion.service';

@Component({
    selector: 'app-event-detail',
    templateUrl: './event-detail.component.html',
    styleUrl: './event-detail.component.scss',
    standalone: false
})
export class EventDetailComponent implements OnInit, OnDestroy {
  private readonly sanitizer = inject(DomSanitizer);
  private readonly todayEvent = inject(TodayEventService);
  private readonly platformId = inject(PLATFORM_ID);
  private readonly pendingTasks = inject(PendingTasks);
  private readonly preferences = inject(HalloweenPreferences);
  private readonly motion = inject(TodayBannerHalloweenMotionService);
  private runnerTimer: ReturnType<typeof setTimeout> | undefined;
  protected readonly calm = this.preferences.calm;
  protected readonly visible = this.preferences.visible;
  protected readonly runnerActive = signal(false);

  /** E6 : le jour du loto, l'encart « Tableau en direct » en tête de fiche. */
  liveLoto = computed(() => {
    const today = this.todayEvent.today();
    const event = this.vpdEvent();
    return today !== null && event !== null && today.id === event.id && today.eventType === 'Bingo'
      && parisDay(today.dateStart) === parisDay(new Date())
      ? today
      : null;
  });

  vpdEvent = signal<VpdEventModel | null>(null)
  isLoading = signal(true);
  protected readonly VpdEventEnum = VpdEventEnum;

  /** Famille de l'évènement, affichée en chapeau du hero au-dessus du titre de l'édition. */
  kicker = computed(() => {
    switch (this.vpdEvent()?.eventType) {
      case VpdEventEnum.Bingo:
        return 'Loto';
      case VpdEventEnum.Books:
        return 'La bourse aux livres';
      default:
        return 'Évènement';
    }
  });

  /**
   * Une édition n'est marquée passée qu'à la fin de sa dernière journée : les dates de
   * l'API portent l'heure de début, comparer directement à l'instant présent basculerait
   * le badge sur "passé" au beau milieu de l'évènement.
   */
  isPast = computed(() => {
    const event = this.vpdEvent();
    if (event === null) return false;

    const lastDay = new Date(event.dateEnd ?? event.dateStart);
    lastDay.setUTCHours(23, 59, 59, 999);
    return lastDay.getTime() < Date.now();
  });

  mapsUrl = computed(() => {
    const event = this.vpdEvent();
    return event === null ? '' : eventMapsUrl(event);
  });

  address = computed(() => {
    const event = this.vpdEvent();
    return event === null ? '' : formatEventAddress(event);
  });

  mapsEmbedUrl = computed<SafeResourceUrl>(() => {
    const event = this.vpdEvent();
    return this.sanitizer.bypassSecurityTrustResourceUrl(
      event === null ? 'about:blank' : eventMapsEmbedUrl(event),
    );
  });

  eventStartTime = computed(() => {
    const event = this.vpdEvent();
    if (event === null) return null;

    return event.hourOpenDoors ?? new Date(event.dateStart);
  });

  protected countdown(event: VpdEventModel): string | null {
    return countdownLabel(event, new Date());
  }

  protected identity(event: VpdEventModel): EventIdentity {
    return eventIdentity(event);
  }

  constructor(private eventsFacadeService: VpdEventsFacadeService,
              private route: ActivatedRoute) {
    effect(() => {
      const event = this.vpdEvent();
      if (this.preferences.calm()) {
        this.runnerActive.set(false);
        clearTimeout(this.runnerTimer);
        return;
      }
      if (!event || this.identity(event) !== 'halloween' || !isPlatformBrowser(this.platformId) || !this.motion.claimEntrance()) {
        return;
      }
      this.runnerActive.set(true);
      this.runnerTimer = setTimeout(() => this.runnerActive.set(false), 3200);
    });
  }

  ngOnInit(): void {
    void this.todayEvent.start();
    this.getEvents()
  }

  ngOnDestroy(): void {
    clearTimeout(this.runnerTimer);
  }

  getEvents() {
    this.route.paramMap.subscribe(params => {
      const id = params.get('id');
      if (id !== null) {
        const finishSsrTask = trackSsrDataRequest(this.pendingTasks, this.platformId);
        this.eventsFacadeService.getEventById(id)
          .then(response => {
            const data: any = response;
            response.eventType = VpdEventEnum[data.eventType as keyof typeof VpdEventEnum];
            this.vpdEvent.set(response);
          })
          .catch(() => this.vpdEvent.set(null))
          .finally(() => {
            this.isLoading.set(false);
            finishSsrTask();
          });
      } else {
        this.isLoading.set(false);
      }
    })
  }
}
