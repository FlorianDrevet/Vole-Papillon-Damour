import {Component, input} from '@angular/core';
import {VpdEventModel} from "../../../../shared/models/vpdEvent.model";
import {BackgroundColorEnum} from '../../../../shared/enums/backgroundColor.enum';

@Component({
    selector: 'app-general-infos',
    templateUrl: './general-infos.component.html',
    styleUrl: './general-infos.component.scss',
    host: {class: 'block w-full min-w-0'},
    standalone: false
})
export class GeneralInfosComponent {
  vpdEvent = input.required<VpdEventModel>()

  protected readonly BackgroundColorEnum = BackgroundColorEnum;
}
