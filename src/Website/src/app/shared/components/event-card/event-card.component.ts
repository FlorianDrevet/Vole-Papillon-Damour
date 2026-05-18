import { Component, input } from '@angular/core';
import { VpdEventModel } from '../../models/vpdEvent.model';

/**
 * Wrapper Website autour de `vpd-event-card`. Mode lecture seule.
 */
@Component({
  selector: 'app-event-card',
  templateUrl: './event-card.component.html',
  standalone: false,
})
export class EventCardComponent {
  VpdEvent = input.required<VpdEventModel>();
}
