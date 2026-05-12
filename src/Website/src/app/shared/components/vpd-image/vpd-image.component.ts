import {Component, computed, inject, input} from '@angular/core';
import {ImageOrientationEnum} from "../../enums/imageOrientation.enum";
import {BackgroundColorEnum} from '../../enums/backgroundColor.enum';
import {RotationEnum} from "../../enums/rotation.enum";

@Component({
    selector: 'app-vpd-image',
    templateUrl: './vpd-image.component.html',
    styleUrl: './vpd-image.component.scss',
    standalone: false
})
export class VpdImageComponent {
  highPriorityFetching = input<boolean>(false);
  orientation = input<ImageOrientationEnum>();
  height = input<number>();
  width = input<number>();
  src = input.required<string>();
  rounded = input(false);
  backgroundColor = input<BackgroundColorEnum | null>(null);
  rotation = input<RotationEnum>();

  protected readonly BackgroundColorEnum = BackgroundColorEnum;
  protected readonly RotationEnum = RotationEnum;
  protected readonly ImageOrientationEnum = ImageOrientationEnum;
}
