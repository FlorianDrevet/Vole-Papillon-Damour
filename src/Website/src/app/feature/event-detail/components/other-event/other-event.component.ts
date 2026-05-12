import {Component, input} from '@angular/core';
import {VpdEventModel} from "../../../../shared/models/vpdEvent.model";
import {ImageOrientationEnum} from "../../../../shared/enums/imageOrientation.enum";
import {BackgroundColorEnum} from "../../../../shared/enums/backgroundColor.enum";

@Component({
  selector: 'app-other-event',
  templateUrl: './other-event.component.html',
  styleUrl: './other-event.component.scss'
})
export class OtherEventComponent {

  vpdEvent = input.required<VpdEventModel>()

  protected readonly ImageOrientationEnum = ImageOrientationEnum;
  protected readonly BackgroundColorEnum = BackgroundColorEnum;
}
