import {Component, input} from '@angular/core';
import {VpdEventModel} from "../../../../shared/models/vpdEvent.model";

@Component({
    selector: 'app-other-event',
    templateUrl: './other-event.component.html',
    styleUrl: './other-event.component.scss',
    standalone: false
})
export class OtherEventComponent {
  vpdEvent = input.required<VpdEventModel>()
}
