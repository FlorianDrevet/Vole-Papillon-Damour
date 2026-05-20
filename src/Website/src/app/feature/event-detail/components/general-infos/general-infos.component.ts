import {Component, input} from '@angular/core';
import {VpdEventModel} from "../../../../shared/models/vpdEvent.model";
import { ImageOrientationEnum } from '../../../../shared/enums/imageOrientation.enum';
import { BackgroundColorEnum } from '../../../../shared/enums/backgroundColor.enum';

@Component({
    selector: 'app-general-infos',
    templateUrl: './general-infos.component.html',
    styleUrl: './general-infos.component.scss',
    standalone: false
})
export class GeneralInfosComponent {
  vpdEvent = input.required<VpdEventModel>()

  protected readonly ImageOrientationEnum = ImageOrientationEnum;
  protected readonly BackgroundColorEnum = BackgroundColorEnum;
}
