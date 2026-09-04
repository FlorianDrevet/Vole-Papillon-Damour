import {Component, computed, inject, OnInit, signal} from '@angular/core';
import {DomSanitizer, SafeResourceUrl} from '@angular/platform-browser';
import {ActivatedRoute} from "@angular/router";
import {VpdEventsFacadeService} from "../../shared/facades/vpd-events.facade.service";
import {VpdEventModel} from "../../shared/models/vpdEvent.model";
import {VpdEventEnum} from "../../shared/enums/vpdEvent.enum";
import {eventMapsEmbedUrl, eventMapsUrl, formatEventAddress} from "../../shared/utils/event-address.util";

@Component({
    selector: 'app-event-detail',
    templateUrl: './event-detail.component.html',
    standalone: false
})
export class EventDetailComponent implements OnInit {
  private readonly sanitizer = inject(DomSanitizer);

  vpdEvent = signal<VpdEventModel | null>(null)
  isLoading = signal(true);
  protected readonly VpdEventEnum = VpdEventEnum;

  /** Famille de l'évènement, affichée en chapeau du hero au-dessus du titre de l'édition. */
  kicker = computed(() => {
    switch (this.vpdEvent()?.eventType) {
      case VpdEventEnum.Bingo:
        return 'Le grand loto';
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

    if (event.eventType === VpdEventEnum.Books) {
      return event.hourOpenDoors ?? new Date(event.dateStart);
    }

    return new Date(event.dateStart);
  });

  constructor(private eventsFacadeService: VpdEventsFacadeService,
              private route: ActivatedRoute) {
  }

  ngOnInit(): void {
    this.getEvents()
  }

  getEvents() {
    this.route.paramMap.subscribe(params => {
      const id = params.get('id');
      if (id !== null) {
        this.eventsFacadeService.getEventById(id)
          .then(response => {
            const data: any = response;
            response.eventType = VpdEventEnum[data.eventType as keyof typeof VpdEventEnum];
            this.vpdEvent.set(response);
          })
          .catch(() => this.vpdEvent.set(null))
          .finally(() => this.isLoading.set(false));
      } else {
        this.isLoading.set(false);
      }
    })
  }
}
