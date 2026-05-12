import { Component } from '@angular/core';
import {ImageOrientationEnum} from "../../../shared/enums/imageOrientation.enum";
import {BackgroundColorEnum} from "../../../shared/enums/backgroundColor.enum";

@Component({
    selector: 'app-presentation',
    templateUrl: './presentation.component.html',
    styleUrl: './presentation.component.scss',
    standalone: false
})
export class PresentationComponent {

  protected readonly ImageOrientationEnum = ImageOrientationEnum;
  protected readonly BackgroundColorEnum = BackgroundColorEnum;
}
