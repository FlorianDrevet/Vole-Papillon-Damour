import {Component, inject, OnInit, signal} from '@angular/core';
import {VpdEventsFacadeService} from "../../shared/facades/vpd-events.facade.service";
import {VpdEventModel} from "../../shared/models/vpdEvent.model";
import {VpdEventEnum} from "../../shared/enums/vpdEvent.enum";
import {MatDialog} from "@angular/material/dialog";
import {
  CreateUpdateEventDialogComponent
} from "../../shared/components/dialogs/create-update-event-dialog/create-update-event-dialog.component";
import {
  ScanBingoCardDialogComponent
} from "../../shared/components/dialogs/scan-bingo-card-dialog/scan-bingo-card-dialog.component";

@Component({
  selector: 'app-vpd-events',
  templateUrl: './vpd-events.component.html',
  styleUrl: './vpd-events.component.scss'
})
export class VpdEventsComponent implements OnInit{
  private readonly vpdEventsFacade = inject(VpdEventsFacadeService);
  private readonly dialog = inject(MatDialog);

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

  openDialogScanBingoCard() {
    const dialogRef = this.dialog.open(ScanBingoCardDialogComponent, {
      "maxWidth": "90vw",
      "width": "fit-content",
      "height": "fit-content",
    });
  }

  openDialogCreation() {
    const dialogRef = this.dialog.open(CreateUpdateEventDialogComponent, {
      "maxWidth": "90vw",
      "width": "fit-content",
      "height": "fit-content",
    });

    dialogRef.afterClosed().subscribe((result: VpdEventModel) => {
      switch (result.eventType) {
        case VpdEventEnum.Bingo:
          this.allBingoEvents.set([...this.allBingoEvents(), result]);
          break;
        case VpdEventEnum.Books:
          this.allBooksEvents.set([...this.allBooksEvents(), result]);
          break;
        case VpdEventEnum.Other:
          this.allOtherEvents.set([...this.allOtherEvents(), result]);
          break;
        default:
          console.log("NO TYPE FOUND")
          break
      }
    });
  }

  onEventDeleted($event: VpdEventModel) {
    switch ($event.eventType) {
      case VpdEventEnum.Bingo:
        this.allBingoEvents.set(this.allBingoEvents().filter(event => event.id !== $event.id));
        break;
      case VpdEventEnum.Books:
        this.allBooksEvents.set(this.allBooksEvents().filter(event => event.id !== $event.id));
        break;
      case VpdEventEnum.Other:
        this.allOtherEvents.set(this.allBooksEvents().filter(event => event.id !== $event.id));
        break;
      default:
        console.log("NO TYPE FOUND")
        break
    }
  }

  onEventUpdated($event: VpdEventModel) {
    switch ($event.eventType) {
      case VpdEventEnum.Bingo:
        this.allBingoEvents.set(this.allBingoEvents().map(event => {
          if (event.id === $event.id) {
            return $event
          }
          return event
        }));
        break;
      case VpdEventEnum.Books:
        this.allBooksEvents.set(this.allBooksEvents().map(event => {
          if (event.id === $event.id) {
            return $event
          }
          return event
        }));
        break;
      case VpdEventEnum.Other:
        this.allOtherEvents.set(this.allOtherEvents().map(event => {
          if (event.id === $event.id) {
            return $event
          }
          return event
        }));
        break;
      default:
        console.log("NO TYPE FOUND")
        break
    }
  }
}
