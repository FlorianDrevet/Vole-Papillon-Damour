import {Component, inject, OnInit, signal} from '@angular/core';
import {VpdEventsFacadeService} from "../../shared/facades/vpd-events.facade.service";
import {VpdEventModel} from "../../shared/models/vpdEvent.model";
import {VpdEventEnum} from "../../shared/enums/vpdEvent.enum";

@Component({
    selector: 'app-vpd-all-events',
    templateUrl: './vpd-all-events.component.html',
    standalone: false
})
export class VpdAllEventsComponent implements OnInit{
  private readonly vpdEventsFacade = inject(VpdEventsFacadeService);

  allBingoEvents = signal<VpdEventModel[]>([])
  allBooksEvents = signal<VpdEventModel[]>([])
  allOtherEvents = signal<VpdEventModel[]>([])
  isLoading = signal(true);
  readonly loadingCards = [0, 1, 2];

  ngOnInit(): void {
    this.vpdEventsFacade.getAllEvents$().then((events : any[]) => {
      const allEvents = events ?? [];
      allEvents.forEach(event => {
        event.eventType = VpdEventEnum[event.eventType as keyof typeof VpdEventEnum];
      });
      this.allBingoEvents.set(allEvents.filter(event => event.eventType === VpdEventEnum.Bingo));
      this.allBooksEvents.set(allEvents.filter(event => event.eventType === VpdEventEnum.Books));
      this.allOtherEvents.set(allEvents.filter(event => event.eventType === VpdEventEnum.Other));
    })
      .catch(() => undefined)
      .finally(() => this.isLoading.set(false));
  }
}
