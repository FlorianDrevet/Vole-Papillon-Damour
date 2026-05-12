import {Component, inject, OnInit, signal} from '@angular/core';
import {VpdEventsFacadeService} from "../../shared/facades/vpd-events.facade.service";
import {VpdEventModel} from "../../shared/models/vpdEvent.model";
import {VpdEventEnum} from "../../shared/enums/vpdEvent.enum";

@Component({
    selector: 'app-vpd-events',
    templateUrl: './vpd-all-events.component.html',
    styleUrl: './vpd-all-events.component.scss',
    standalone: false
})
export class VpdAllEventsComponent implements OnInit{
  private readonly vpdEventsFacade = inject(VpdEventsFacadeService);

  allBingoEvents = signal<VpdEventModel[]>([])
  allBooksEvents = signal<VpdEventModel[]>([])
  allOtherEvents = signal<VpdEventModel[]>([])
  isLoading = signal(true);

  ngOnInit(): void {
    this.vpdEventsFacade.getAllEvents$().then((events : any[]) => {
      events.map(event => {
        event.eventType = VpdEventEnum[event.eventType as keyof typeof VpdEventEnum];
        return
      });
      this.allBingoEvents.set(events.filter(event => event.eventType === VpdEventEnum.Bingo));
      this.allBooksEvents.set(events.filter(event => event.eventType === VpdEventEnum.Books));
      this.allOtherEvents.set(events.filter(event => event.eventType === VpdEventEnum.Other));
      this.isLoading.set(false);
    })
  }
}
