import { Component } from '@angular/core';
import {ImageOrientationEnum} from "../../../../../shared/enums/imageOrientation.enum";
import {BackgroundColorEnum} from "../../../../../shared/enums/backgroundColor.enum";

@Component({
  selector: 'app-year-2010',
  templateUrl: './year-2010.component.html',
  styleUrl: './year-2010.component.scss'
})
export class Year2010Component {

  protected readonly ImageOrientationEnum = ImageOrientationEnum;
  protected readonly BackgroundColorEnum = BackgroundColorEnum;
}
