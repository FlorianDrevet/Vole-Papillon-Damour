import { Component } from '@angular/core';
import {ImageOrientationEnum} from "../../../../../shared/enums/imageOrientation.enum";
import {BackgroundColorEnum} from "../../../../../shared/enums/backgroundColor.enum";

@Component({
  selector: 'app-year-2006',
  templateUrl: './year-2006.component.html',
  styleUrl: './year-2006.component.scss'
})
export class Year2006Component {

  protected readonly ImageOrientationEnum = ImageOrientationEnum;
  protected readonly BackgroundColorEnum = BackgroundColorEnum;
}
