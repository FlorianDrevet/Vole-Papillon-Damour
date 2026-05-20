import { Component, inject, input, output } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { VpdEventModel } from '../../models/vpdEvent.model';
import { ConfirmationDialogComponent } from '../dialogs/confirmation-dialog/confirmation-dialog.component';
import { CreateUpdateEventDialogComponent } from '../dialogs/create-update-event-dialog/create-update-event-dialog.component';
import { VpdEventsFacadeService } from '../../facades/vpd-events.facade.service';

/**
 * Wrapper "smart" autour du composant `vpd-event-card` du design system.
 * Conserve le selector `app-event-card` historique.
 */
@Component({
  selector: 'app-event-card',
  templateUrl: './event-card.component.html',
  standalone: false,
})
export class EventCardComponent {
  eventDeleted = output<VpdEventModel>();
  eventUpdated = output<VpdEventModel>();

  VpdEvent = input.required<VpdEventModel>();

  readonly dialog = inject(MatDialog);
  readonly eventsFacade = inject(VpdEventsFacadeService);
  private readonly _snackBar = inject(MatSnackBar);

  openDialogUpdate(): void {
    const dialogRef = this.dialog.open(CreateUpdateEventDialogComponent, {
      data: this.VpdEvent(),
      maxWidth: '90vw',
      width: 'fit-content',
      height: 'fit-content',
    });

    dialogRef.afterClosed().subscribe((result) => {
      if (result !== null) {
        this.eventUpdated.emit(result);
      }
    });
  }

  openDialogDeletion(): void {
    const dialogRef = this.dialog.open(ConfirmationDialogComponent, {
      data: { title: 'Êtes-vous sûr de vouloir supprimer cet évènement ?' },
    });

    dialogRef.afterClosed().subscribe((result) => {
      if (result) {
        this.eventsFacade.deleteEventById$(this.VpdEvent().id).then(() => {
          this._snackBar.open('Evènement supprimé avec succès', 'Fermer', {
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
}
