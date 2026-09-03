import {Component, computed, inject, OnInit, signal} from '@angular/core';
import {VpdEventsFacadeService} from "../../shared/facades/vpd-events.facade.service";
import {VpdEventModel} from "../../shared/models/vpdEvent.model";
import {VpdEventEnum} from "../../shared/enums/vpdEvent.enum";
import {EVENT_EDITORIAL_PHOTOS} from '../../shared/data/event-editorial-content';

/**
 * Page « Évènements ».
 *
 * Elle met en avant les deux piliers de la saison (loto et bourse aux livres) avec
 * leurs photos et leur description permanentes, puis liste en dessous tous les autres
 * rendez-vous. Un seul appel à `/asso-events` (évènements à venir, triés par date)
 * alimente les trois blocs : il donne la prochaine occurrence de chaque pilier *et*
 * le nombre de dates restantes, ce que les endpoints `next-*` ne permettaient pas.
 */
@Component({
    selector: 'app-vpd-events',
    templateUrl: './vpd-events-page.component.html',
    standalone: false
})
export class VpdEventsPageComponent implements OnInit {
  private readonly eventFacade = inject(VpdEventsFacadeService);

  /** Photos et points clés des deux piliers : contenu éditorial, indépendant de l'API. */
  protected readonly lotoPhotos = EVENT_EDITORIAL_PHOTOS[VpdEventEnum.Bingo];
  protected readonly lotoHighlights = [
    'Des dizaines de lots offerts par nos partenaires.',
    'Une après-midi en famille, ouverte à tous.',
    'Les bénéfices servent aux besoins de Maxence et au soutien d’autres jeunes.',
  ];
  protected readonly booksPhotos = EVENT_EDITORIAL_PHOTOS[VpdEventEnum.Books];
  protected readonly booksHighlights = [
    'Des milliers d\'ouvrages triés par catégories : romans, jeunesse, BD, régionalisme.',
    'Petits prix, du livre de poche au beau livre.',
    'Vos livres sont acceptés toute l\'année au dépôt de Saint-Just-Saint-Rambert.',
  ];

  private readonly events = signal<VpdEventModel[]>([]);
  protected readonly isLoading = signal(true);
  protected readonly loadingCards = [0, 1, 2];

  private readonly bingoEvents = computed(() => this.eventsOfType(VpdEventEnum.Bingo));
  private readonly booksEvents = computed(() => this.eventsOfType(VpdEventEnum.Books));

  protected readonly nextBingo = computed<VpdEventModel | null>(() => this.bingoEvents()[0] ?? null);
  protected readonly nextBooks = computed<VpdEventModel | null>(() => this.booksEvents()[0] ?? null);
  protected readonly bingoCount = computed(() => this.bingoEvents().length);
  protected readonly booksCount = computed(() => this.booksEvents().length);

  /** Tout ce qui n'est ni un loto ni une bourse aux livres, dans l'ordre chronologique. */
  protected readonly otherEvents = computed(() => this.events().filter(event =>
    event.eventType !== VpdEventEnum.Bingo && event.eventType !== VpdEventEnum.Books));

  ngOnInit(): void {
    this.eventFacade.getAllEvents$()
      .then((events: any[]) => {
        this.events.set((events ?? [])
          .map(event => this.toVpdEvent(event))
          .sort((a, b) => a.dateStart.getTime() - b.dateStart.getTime()));
      })
      .catch(() => undefined)
      .finally(() => this.isLoading.set(false));
  }

  private eventsOfType(eventType: VpdEventEnum): VpdEventModel[] {
    return this.events().filter(event => event.eventType === eventType);
  }

  /** L'API renvoie des dates et un type d'évènement sérialisés en chaînes. */
  private toVpdEvent(event: any): VpdEventModel {
    return {
      ...event,
      dateStart: new Date(event.dateStart),
      dateEnd: event.dateEnd ? new Date(event.dateEnd) : null,
      hourOpenDoors: event.hourOpenDoors ? new Date(event.hourOpenDoors) : null,
      hourCloseDoors: event.hourCloseDoors ? new Date(event.hourCloseDoors) : null,
      eventType: VpdEventEnum[event.eventType as keyof typeof VpdEventEnum]
    };
  }
}
