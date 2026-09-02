import {Component, computed, input} from '@angular/core';
import {VpdEventModel} from "../../../../shared/models/vpdEvent.model";
import {EVENT_EDITORIAL_PHOTOS} from '../../../../shared/data/event-editorial-content';

/**
 * Bande "en pratique" de la page de détail : la description de l'évènement d'un côté,
 * une galerie de photos éditoriales de l'autre.
 */
@Component({
    selector: 'app-general-infos',
    templateUrl: './general-infos.component.html',
    styleUrl: './general-infos.component.scss',
    standalone: false
})
export class GeneralInfosComponent {
  vpdEvent = input.required<VpdEventModel>()

  eventPhotos = computed(() => {
    const event = this.vpdEvent();
    return EVENT_EDITORIAL_PHOTOS[event.eventType] ?? [];
  })
}
