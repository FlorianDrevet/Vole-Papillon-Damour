import {Component, computed, input} from '@angular/core';
import {ImageOrientationEnum} from "../../../../shared/enums/imageOrientation.enum";
import {BackgroundColorEnum} from "../../../../shared/enums/backgroundColor.enum";

@Component({
  selector: 'app-special-event',
  templateUrl: './special-event.component.html',
  styleUrl: './special-event.component.scss'
})
export class SpecialEventComponent {
  Title = input.required<string>()
  Date = input<Date | null>()
  DateEnd = input<Date | null>()
  UrlImage = input.required<string>()
  IdEvent = input<string | null>()
  UrlMoreDetail = computed(() =>  `/evenement/${this.IdEvent()}`)
  protected readonly ImageOrientationEnum = ImageOrientationEnum;
  protected readonly BackgroundColorEnum = BackgroundColorEnum;

  constructor() {
    console.log(this.Date())
  }
}
