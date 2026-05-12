import { Component } from '@angular/core';
import {ImageOrientationEnum} from "../../../../../shared/enums/imageOrientation.enum";
import {BackgroundColorEnum} from "../../../../../shared/enums/backgroundColor.enum";

@Component({
  selector: 'app-year-2008',
  templateUrl: './year-2008.component.html',
  styleUrl: './year-2008.component.scss'
})
export class Year2008Component {

  protected readonly ImageOrientationEnum = ImageOrientationEnum;
  protected readonly BackgroundColorEnum = BackgroundColorEnum;
}
