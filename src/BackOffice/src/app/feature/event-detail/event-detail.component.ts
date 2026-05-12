import {Component, OnInit, signal} from '@angular/core';
import {ActivatedRoute} from "@angular/router";
import {VpdEventsFacadeService} from "../../shared/facades/vpd-events.facade.service";
import {VpdEventModel} from "../../shared/models/vpdEvent.model";
import {VpdEventEnum} from "../../shared/enums/vpdEvent.enum";

@Component({
    selector: 'app-event-detail',
    templateUrl: './event-detail.component.html',
    styleUrl: './event-detail.component.scss',
    standalone: false
})
export class EventDetailComponent implements OnInit {
  vpdEvent = signal<VpdEventModel | null>(null)
  protected readonly VpdEventEnum = VpdEventEnum;

  constructor(private eventsFacadeService: VpdEventsFacadeService,
              private route: ActivatedRoute) {
  }

  ngOnInit(): void {
    this.getEvents()
  }

  getEvents() {
    this.route.paramMap.subscribe(params => {
      if (params.get('id') !== null) {
        this.eventsFacadeService.getEventById$(params.get('id')!).then(response => {
          let data: any = response
          response.eventType = VpdEventEnum[data.eventType as keyof typeof VpdEventEnum];
          this.vpdEvent.set(response)
          console.log(response)
        })
      }
    })
  }
}
