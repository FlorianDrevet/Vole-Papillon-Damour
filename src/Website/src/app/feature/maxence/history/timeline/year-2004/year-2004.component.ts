import { Component } from '@angular/core';
import {BackgroundColorEnum} from "../../../../../shared/enums/backgroundColor.enum";
import {ImageOrientationEnum} from "../../../../../shared/enums/imageOrientation.enum";

@Component({
  selector: 'app-year-2004',
  templateUrl: './year-2004.component.html',
  styleUrl: './year-2004.component.scss'
})
export class Year2004Component {

    protected readonly BackgroundColorEnum = BackgroundColorEnum;
  protected readonly ImageOrientationEnum = ImageOrientationEnum;
}
