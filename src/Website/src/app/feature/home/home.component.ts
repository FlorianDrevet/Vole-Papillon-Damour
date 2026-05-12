import {Component, computed, OnInit, signal} from '@angular/core';
import {BackgroundColorEnum} from "../../shared/enums/backgroundColor.enum";
import {ImageOrientationEnum} from "../../shared/enums/imageOrientation.enum";
import {RotationEnum} from "../../shared/enums/rotation.enum";
import {VpdEventModel} from "../../shared/models/vpdEvent.model";
import {AxiosService} from "../../shared/services/axios.service";
import {MethodEnum} from "../../shared/enums/method.enum";
import {VpdEventEnum} from "../../shared/enums/vpdEvent.enum";

@Component({
    selector: 'app-home',
    templateUrl: './home.component.html',
    styleUrl: './home.component.scss',
    standalone: false
})
export class HomeComponent implements OnInit{
  //TODO
  lotoCard = signal<VpdEventModel | null>(null)
  todayDate = new Date();

  isToday = computed(() => {
    if (this.lotoCard() !== null) {
      const date = new Date(this.lotoCard()!.dateStart)
      return date.getDate() === this.todayDate.getDate()
        && date.getMonth() === this.todayDate.getMonth()
        &&date.getFullYear() === this.todayDate.getFullYear();
    }
    return false;
  })

  constructor(private axiosService: AxiosService) {
  }

  ngOnInit(): void {
    this.axiosService.request(MethodEnum.GET, '/asso-events/next-bingo', {})
      .then((data: any) => {
        data.date = new Date(data.date);
        data.eventType = VpdEventEnum[data.eventType as keyof typeof VpdEventEnum];
        this.lotoCard.set(data)
        console.log(data)
      });
  }

  protected readonly BackgroundColorEnum = BackgroundColorEnum;
  protected readonly ImageOrientationEnum = ImageOrientationEnum;
  protected readonly RotationEnum = RotationEnum;
  protected readonly Date = Date;
}
