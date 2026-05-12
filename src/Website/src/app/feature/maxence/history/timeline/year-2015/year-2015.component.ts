import { Component } from '@angular/core';
import {ImageOrientationEnum} from "../../../../../shared/enums/imageOrientation.enum";
import {BackgroundColorEnum} from "../../../../../shared/enums/backgroundColor.enum";

@Component({
  selector: 'app-year-2015',
  templateUrl: './year-2015.component.html',
  styleUrl: './year-2015.component.scss'
})
export class Year2015Component {

  protected readonly ImageOrientationEnum = ImageOrientationEnum;
  protected readonly BackgroundColorEnum = BackgroundColorEnum;
}
