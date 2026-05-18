import {Component, OnInit, signal} from '@angular/core';
import {VpdEventModel} from "../../../../shared/models/vpdEvent.model";
import {VpdEventEnum} from "../../../../shared/enums/vpdEvent.enum";
import {AxiosService} from "../../../../shared/services/axios.service";
import {MethodEnum} from "../../../../shared/enums/method.enum";

@Component({
    selector: 'app-vpd-events-section',
    templateUrl: './vpd-event-sections.html',
    styleUrl: './vpd-event-sections.scss',
    standalone: false
})
export class VpdEventSections implements OnInit {
  lotoCard = signal<VpdEventModel | null>(null)
  balCard = signal<VpdEventModel | null>(null)
  otherCard = signal<VpdEventModel[]>([])

  constructor(private axiosService: AxiosService) {
  }

  ngOnInit(): void {
    this.axiosService.request(MethodEnum.GET, '/asso-events/next-bingo', {})
      .then((data: any) => {
        data.date = new Date(data.date);
        data.eventType = VpdEventEnum[data.eventType as keyof typeof VpdEventEnum];
        this.lotoCard.set(data)
      })
      .catch(() => undefined);

    this.axiosService.request(MethodEnum.GET, '/asso-events/next-books', {})
      .then((data: any) => {
        data.date = new Date(data.date);
        data.eventType = VpdEventEnum[data.eventType as keyof typeof VpdEventEnum];
        this.balCard.set(data)
      })
      .catch(() => undefined);


    this.axiosService.request(MethodEnum.GET, '/asso-events/next-other-event', {})
      .then((data: any[]) => {
        data.map(data => {
          data.date = new Date(data.date);
          data.eventType = VpdEventEnum[data.eventType as keyof typeof VpdEventEnum];
          return data
        })
        this.otherCard.set(data)
      })
      .catch(() => undefined)
  }
}
