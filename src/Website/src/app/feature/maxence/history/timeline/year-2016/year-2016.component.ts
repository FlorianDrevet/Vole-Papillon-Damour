import { Component } from '@angular/core';
import {BackgroundColorEnum} from "../../../../../shared/enums/backgroundColor.enum";
import {ImageOrientationEnum} from "../../../../../shared/enums/imageOrientation.enum";

@Component({
    selector: 'app-year-2016',
    templateUrl: './year-2016.component.html',
    styleUrl: './year-2016.component.scss',
    standalone: false
})
export class Year2016Component {

  protected readonly BackgroundColorEnum = BackgroundColorEnum;
  protected readonly ImageOrientationEnum = ImageOrientationEnum;
}
