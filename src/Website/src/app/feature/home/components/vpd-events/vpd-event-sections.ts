import {Component, OnInit, signal} from '@angular/core';
import {VpdEventModel} from "../../../../shared/models/vpdEvent.model";
import {VpdEventEnum} from "../../../../shared/enums/vpdEvent.enum";
import {AxiosService} from "../../../../shared/services/axios.service";
import {MethodEnum} from "../../../../shared/enums/method.enum";

/** Nombre de cartes de la bande « Prochains rendez-vous » sur l'accueil. */
const UPCOMING_EVENTS_COUNT = 3;

@Component({
    selector: 'app-vpd-events-section',
    templateUrl: './vpd-event-sections.html',
    styleUrl: './vpd-event-sections.scss',
    standalone: false
})
export class VpdEventSections implements OnInit {
  /**
   * Les trois prochains rendez-vous, tous types confondus et dans l'ordre chronologique :
   * la bande peut donc afficher trois lotos d'affilée. Auparavant elle figeait « le prochain
   * loto + la prochaine bourse aux livres + les autres évènements », ce qui remontait des
   * dates lointaines devant des rendez-vous bien plus proches.
   */
  upcomingEvents = signal<VpdEventModel[]>([])
  readonly isLoading = signal(true);
  readonly loadingCards = [0, 1, 2];

  constructor(private axiosService: AxiosService) {
  }

  /**
   * Inclinaison et cadence du flottement des cartes.
   * La maquette fige trois cartes (-1.4deg / 1.1deg / -0.7deg, 7s / 7.6s / 8.2s) ; la série
   * est prolongée au-delà de la troisième en alternant le sens, au cas où le nombre de cartes
   * augmenterait, pour éviter que les cartes ne montent et descendent en même temps.
   */
  private static readonly floatRotationsDeg = [-1.4, 1.1, -0.7];

  protected cardRotationDeg(index: number): number {
    return VpdEventSections.floatRotationsDeg[index] ?? (index % 2 === 0 ? -0.7 : 1.2);
  }

  protected cardDurationS(index: number): number {
    return 7 + index * 0.6;
  }

  /**
   * Décalage appliqué en délai *négatif* : l'animation démarre déjà en cours, donc la
   * carte est inclinée et flotte dès la première frame. Avec un délai positif elle
   * resterait droite et immobile avant de basculer d'un coup sur son inclinaison.
   */
  protected cardDelayS(index: number): number {
    return -(index * 0.7);
  }

  /**
   * Reprend la même règle que la fiche événement : une bourse porte sa vraie heure
   * d'ouverture dans `hourOpenDoors`, tandis que les autres événements portent leur
   * heure de début dans `dateStart`.
   */
  protected eventTime(event: VpdEventModel): Date {
    if (event.eventType === VpdEventEnum.Books) {
      return event.hourOpenDoors ?? new Date(event.dateStart);
    }

    return new Date(event.dateStart);
  }

  ngOnInit(): void {
    // `/asso-events` ne renvoie que les évènements à venir, déjà triés par date de début.
    this.axiosService.request(MethodEnum.GET, '/asso-events', {})
      .then((data: any[]) => {
        const events = (data ?? [])
          .map(event => this.toVpdEvent(event))
          .sort((a, b) => a.dateStart.getTime() - b.dateStart.getTime())
          .slice(0, UPCOMING_EVENTS_COUNT);

        this.upcomingEvents.set(events)
      })
      .catch(() => undefined)
      .finally(() => this.isLoading.set(false));
  }

  private toVpdEvent(event: any): VpdEventModel {
    return {
      ...event,
      dateStart: new Date(event.dateStart),
      dateEnd: event.dateEnd ? new Date(event.dateEnd) : null,
      hourOpenDoors: event.hourOpenDoors ? new Date(event.hourOpenDoors) : null,
      eventType: VpdEventEnum[event.eventType as keyof typeof VpdEventEnum]
    };
  }
}
