import { Component } from '@angular/core';
import {ImageOrientationEnum} from "../../../../../shared/enums/imageOrientation.enum";
import {BackgroundColorEnum} from "../../../../../shared/enums/backgroundColor.enum";

@Component({
  selector: 'app-year-2014',
  templateUrl: './year-2014.component.html',
  styleUrl: './year-2014.component.scss'
})
export class Year2014Component {

  protected readonly ImageOrientationEnum = ImageOrientationEnum;
  protected readonly BackgroundColorEnum = BackgroundColorEnum;
}
