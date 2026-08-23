import {Component, OnInit, signal} from '@angular/core';
import {VpdEventsFacadeService} from "../../shared/facades/vpd-events.facade.service";
import {VpdEventModel} from "../../shared/models/vpdEvent.model";

@Component({
    selector: 'app-vpd-events',
    templateUrl: './vpd-events-page.component.html',
    standalone: false
})
export class VpdEventsPageComponent implements OnInit{
  constructor(private eventFacade: VpdEventsFacadeService) {
  }

  nextBingo = signal<VpdEventModel | null>(null)
  nextBooks = signal<VpdEventModel | null>(null)
  nextOthers = signal<VpdEventModel[]>([])

  ngOnInit(): void {
    this.eventFacade.getLatestEventBingo().then(e => this.nextBingo.set(e)).catch(() => undefined);
    this.eventFacade.getLatestEventBooks().then(e => this.nextBooks.set(e)).catch(() => undefined);
    this.eventFacade.getLatestEventOthers().then(e => this.nextOthers.set(e)).catch(() => undefined);
  }
}
