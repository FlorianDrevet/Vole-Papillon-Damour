import {Component, computed, input} from '@angular/core';
import {VpdEventModel} from "../../../../shared/models/vpdEvent.model";
import {eventMapsUrl, formatEventAddress} from "../../../../shared/utils/event-address.util";

/**
 * Bande "en pratique" de la page de détail : la description de l'évènement d'un côté,
 * la carte récapitulative de l'autre. Les lignes propres au type d'évènement (date,
 * horaires) sont projetées par le composant appelant ; la ligne "Lieu", identique pour
 * tous les types, est rendue ici pour n'exister qu'à un seul endroit.
 */
@Component({
    selector: 'app-general-infos',
    templateUrl: './general-infos.component.html',
    styleUrl: './general-infos.component.scss',
    standalone: false
})
export class GeneralInfosComponent {
  vpdEvent = input.required<VpdEventModel>()

  /** Plan d'accès optionnel, affiché sous l'adresse (bourse aux livres). */
  mapImage = input<string | null>(null)

  address = computed(() => formatEventAddress(this.vpdEvent()))
  mapsUrl = computed(() => eventMapsUrl(this.vpdEvent()))
}
