import {Component, OnInit, signal} from '@angular/core';
import {VpdEventModel} from "../../../../shared/models/vpdEvent.model";
import {VpdEventEnum} from "../../../../shared/enums/vpdEvent.enum";
import {AxiosService} from "../../../../shared/services/axios.service";
import {MethodEnum} from "../../../../shared/enums/method.enum";

@Component({
    selector: 'app-vpd-events-section',
    templateUrl: './vpd-event-sections.html',
    styleUrl: './vpd-event-sections.scss',
    standalone: false
})
export class VpdEventSections implements OnInit {
  lotoCard = signal<VpdEventModel | null>(null)
  balCard = signal<VpdEventModel | null>(null)
  otherCard = signal<VpdEventModel[]>([])

  constructor(private axiosService: AxiosService) {
  }

  /**
   * Inclinaison et cadence du flottement des cartes « autres évènements ».
   * La maquette fige trois cartes (-1.4deg / 1.1deg / -0.7deg, 7s / 7.6s / 8.2s) ;
   * comme le nombre de cartes est variable ici, on prolonge la série au-delà de la
   * troisième en alternant le sens et en décalant durée et départ, pour éviter que
   * les cartes ne montent et descendent en même temps.
   */
  protected otherRotationDeg(index: number): number {
    return index % 2 === 0 ? -0.7 : 1.2;
  }

  protected otherDurationS(index: number): number {
    return 8.2 + index * 0.4;
  }

  /**
   * Décalage appliqué en délai *négatif* : l'animation démarre déjà en cours, donc la
   * carte est inclinée et flotte dès la première frame. Avec un délai positif elle
   * resterait droite et immobile avant de basculer d'un coup sur son inclinaison.
   */
  protected otherDelayS(index: number): number {
    return -(1.4 + index * 0.6);
  }

  ngOnInit(): void {
    this.axiosService.request(MethodEnum.GET, '/asso-events/next-bingo', {})
      .then((data: any) => {
        data.date = new Date(data.date);
        data.eventType = VpdEventEnum[data.eventType as keyof typeof VpdEventEnum];
        this.lotoCard.set(data)
      })
      .catch(() => undefined);

    this.axiosService.request(MethodEnum.GET, '/asso-events/next-books', {})
      .then((data: any) => {
        data.date = new Date(data.date);
        data.eventType = VpdEventEnum[data.eventType as keyof typeof VpdEventEnum];
        this.balCard.set(data)
      })
      .catch(() => undefined);


    this.axiosService.request(MethodEnum.GET, '/asso-events/next-other-event', {})
      .then((data: any[]) => {
        data.map(data => {
          data.date = new Date(data.date);
          data.eventType = VpdEventEnum[data.eventType as keyof typeof VpdEventEnum];
          return data
        })
        this.otherCard.set(data)
      })
      .catch(() => undefined)
  }
}
