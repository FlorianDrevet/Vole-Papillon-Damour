import {Component, computed, inject, input} from '@angular/core';
import {DomSanitizer, SafeResourceUrl} from '@angular/platform-browser';
import {VpdEventModel} from "../../../../shared/models/vpdEvent.model";
import {eventMapsEmbedUrl, eventMapsUrl, formatEventAddress} from "../../../../shared/utils/event-address.util";

/**
 * Bande "en pratique" de la page de détail : la description de l'évènement d'un côté,
 * une carte du lieu de l'autre. Les lignes propres au type d'évènement (date, horaires)
 * sont projetées par le composant appelant.
 */
@Component({
    selector: 'app-general-infos',
    templateUrl: './general-infos.component.html',
    styleUrl: './general-infos.component.scss',
    standalone: false
})
export class GeneralInfosComponent {
  private readonly sanitizer = inject(DomSanitizer)

  vpdEvent = input.required<VpdEventModel>()

  address = computed(() => formatEventAddress(this.vpdEvent()))
  mapsUrl = computed(() => eventMapsUrl(this.vpdEvent()))
  mapsEmbedUrl = computed<SafeResourceUrl>(() =>
    this.sanitizer.bypassSecurityTrustResourceUrl(eventMapsEmbedUrl(this.vpdEvent())),
  )
}
