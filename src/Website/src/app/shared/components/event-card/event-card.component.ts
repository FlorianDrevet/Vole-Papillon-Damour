import {Component, input} from '@angular/core';
import {VpdEventModel} from "../../models/vpdEvent.model";
import {VpdEventEnum} from "../../enums/vpdEvent.enum";

@Component({
  selector: 'app-event-card',
  templateUrl: './event-card.component.html',
  styleUrl: './event-card.component.scss'
})
export class EventCardComponent {
  VpdEvent = input.required<VpdEventModel>()
  protected readonly VpdEventEnum = VpdEventEnum;
}
