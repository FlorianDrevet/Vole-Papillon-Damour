import {Component, inject, model} from '@angular/core';
import {VpdEventModel} from "../../../../shared/models/vpdEvent.model";
import {MatDialog} from "@angular/material/dialog";
import {
  CreateUpdatePartieDataDialogInterface
} from "../../../../shared/interfaces/createUpdatePartieDataDialog.interface";
import {CreationUpdatePartieComponent} from "./components/dialogs/creation-partie/creation-update-partie.component";

@Component({
  selector: 'app-bingo-event',
  templateUrl: './bingo-event.component.html',
  styleUrl: './bingo-event.component.scss'
})
export class BingoEventComponent {
  vpdEvent = model.required<VpdEventModel>()
  readonly dialog = inject(MatDialog);

  openCreatePartie() {
    const dialogRef = this.dialog.open(CreationUpdatePartieComponent, {
      data: {
        partie: null,
        event: this.vpdEvent(),
      } as CreateUpdatePartieDataDialogInterface,

      "maxWidth": "90vw",
      "width": "fit-content",
      "height": "fit-content",
    });

    dialogRef.afterClosed().subscribe((result: VpdEventModel) => {
      if (result !== null) {
        this.vpdEvent.set(result)
      }
    });
  }

  eventUdpated(vpdEvent: VpdEventModel) {
    this.vpdEvent.set(vpdEvent)
  }
}
