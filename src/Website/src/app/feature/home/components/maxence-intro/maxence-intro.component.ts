import { Component } from '@angular/core';
import {RotationEnum} from "../../../../shared/enums/rotation.enum";
import {ImageOrientationEnum} from "../../../../shared/enums/imageOrientation.enum";
import {BackgroundColorEnum} from "../../../../shared/enums/backgroundColor.enum";

@Component({
    selector: 'app-maxence-intro',
    templateUrl: './maxence-intro.component.html',
    styleUrl: './maxence-intro.component.scss',
    standalone: false
})
export class MaxenceIntroComponent {

  protected readonly RotationEnum = RotationEnum;
  protected readonly ImageOrientationEnum = ImageOrientationEnum;
  protected readonly BackgroundColorEnum = BackgroundColorEnum;
}
