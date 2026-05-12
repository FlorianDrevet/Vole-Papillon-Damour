import {Component, inject, input, output} from '@angular/core';
import {VpdEventModel} from "../../models/vpdEvent.model";
import {VpdEventEnum} from "../../enums/vpdEvent.enum";
import {ConfirmationDialogComponent} from "../dialogs/confirmation-dialog/confirmation-dialog.component";
import {MatDialog} from "@angular/material/dialog";
import {MatSnackBar} from "@angular/material/snack-bar";
import {VpdEventsFacadeService} from "../../facades/vpd-events.facade.service";
import {
  CreateUpdateEventDialogComponent
} from "../dialogs/create-update-event-dialog/create-update-event-dialog.component";

@Component({
    selector: 'app-event-card',
    templateUrl: './event-card.component.html',
    styleUrl: './event-card.component.scss',
    standalone: false
})
export class EventCardComponent {
  eventDeleted = output<VpdEventModel>()
  eventUpdated = output<VpdEventModel>()

  VpdEvent = input.required<VpdEventModel>()

  readonly dialog = inject(MatDialog);
  readonly eventsFacade = inject(VpdEventsFacadeService);
  private _snackBar = inject(MatSnackBar);

  openDialogUpdate() {
    const dialogRef = this.dialog.open(CreateUpdateEventDialogComponent, {
      data: this.VpdEvent(),
      "maxWidth": "90vw",
      "width": "fit-content",
      "height": "fit-content",
    });

    dialogRef.afterClosed().subscribe(result => {
      if (result !== null) {
        this.eventUpdated.emit(result);
      }
    });
  }

  openDialogDeletion() {
    const dialogRef = this.dialog.open(ConfirmationDialogComponent, {
      data: {title: "Êtes-vous sûr de vouloir supprimer cet évènement ?"},
    });

    dialogRef.afterClosed().subscribe(result => {
      if (result) {
        this.eventsFacade.deleteEventById$(this.VpdEvent().id).then(() => {
          this._snackBar.open("Evènement supprimé avec succès", 'Fermer', {
            duration: 2000,
            horizontalPosition: 'end',
            verticalPosition: 'top',
          });
          this.eventDeleted.emit(this.VpdEvent());
        }).catch(() => {
          this._snackBar.open("Erreur lors de la suppression de l'évènement", 'Fermer', {
            duration: 2000,
            horizontalPosition: 'end',
            verticalPosition: 'top',
          });
        });
      }
    });

  }

  protected readonly VpdEventEnum = VpdEventEnum;
}
