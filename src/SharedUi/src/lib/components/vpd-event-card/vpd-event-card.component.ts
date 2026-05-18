import { Component, input, output } from '@angular/core';
import { DsVpdEventModel } from '../../models/ds-event.model';
import { VpdEventEnum } from '../../enums/vpd-event.enum';

/**
 * Carte d'événement unifiée.
 *
 * - `editable=false` (défaut) : affichage seul (Website).
 * - `editable=true` : affiche les boutons edit/supprimer + accès tableau bingo.
 */
@Component({
  selector: 'vpd-event-card',
  templateUrl: './vpd-event-card.component.html',
  standalone: false,
})
export class VpdEventCardComponent {
  event = input.required<DsVpdEventModel>();
  editable = input<boolean>(false);

  editRequested = output<DsVpdEventModel>();
  deleteRequested = output<DsVpdEventModel>();

  protected readonly VpdEventEnum = VpdEventEnum;

  onEdit(): void {
    this.editRequested.emit(this.event());
  }

  onDelete(): void {
    this.deleteRequested.emit(this.event());
  }
}
