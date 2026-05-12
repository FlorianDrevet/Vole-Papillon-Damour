import { Component } from '@angular/core';
import {BackgroundColorEnum} from "../../../../../shared/enums/backgroundColor.enum";
import {ImageOrientationEnum} from "../../../../../shared/enums/imageOrientation.enum";

@Component({
  selector: 'app-year-2005',
  templateUrl: './year-2005.component.html',
  styleUrl: './year-2005.component.scss'
})
export class Year2005Component {

    protected readonly BackgroundColorEnum = BackgroundColorEnum;
  protected readonly ImageOrientationEnum = ImageOrientationEnum;
}
