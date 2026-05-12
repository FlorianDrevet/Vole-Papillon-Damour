import {Component, input} from '@angular/core';
import {BackgroundColorEnum} from '../../enums/backgroundColor.enum';

@Component({
  selector: 'app-vpd-image',
  templateUrl: './vpd-image.component.html',
  styleUrl: './vpd-image.component.scss'
})
export class VpdImageComponent {
  height = input<number>();
  width = input<number>();
  src = input.required<string>();
  rounded = input(false);
  backgroundColor = input<BackgroundColorEnum | null>(null);

  protected readonly BackgroundColorEnum = BackgroundColorEnum;
}
