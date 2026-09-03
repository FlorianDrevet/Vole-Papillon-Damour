import { Component, inject, input, output } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { ActualityModel } from '../../models/actuality.model';
import { ConfirmationDialogComponent } from '../dialogs/confirmation-dialog/confirmation-dialog.component';
import { CreateUpdateActualityDialogComponent } from '../dialogs/create-update-actuality-dialog/create-update-actuality-dialog.component';
import { ActualityFacadeService } from '../../facades/actuality.facade.service';

/**
 * Wrapper "smart" autour du composant `vpd-actuality-card` du design system.
 * Conserve le selector `app-actuality-card` historique pour ne pas casser les templates existants.
 * Bride les actions edit/delete du DS aux dialogues et facades du BackOffice.
 */
@Component({
  selector: 'app-actuality-card',
  templateUrl: './actuality-card.component.html',
  standalone: false,
})
export class ActualityCardComponent {
  actualityDeleted = output<string>();
  actualityUpdated = output<ActualityModel>();
  ActualityModel = input.required<ActualityModel>();

  readonly dialog = inject(MatDialog);
  readonly actualityFacade = inject(ActualityFacadeService);
  private readonly _snackBar = inject(MatSnackBar);

  openDialogDeletion(): void {
    const dialogRef = this.dialog.open(ConfirmationDialogComponent, {
      data: { title: 'Êtes-vous sûr de vouloir supprimer cette actualité ?' },
    });

    dialogRef.afterClosed().subscribe((result) => {
      if (result) {
        this.actualityFacade.deleteActualityById$(this.ActualityModel().id).then(() => {
          this._snackBar.open('Actualité supprimée avec succès', 'Fermer', {
            duration: 2000,
            horizontalPosition: 'end',
            verticalPosition: 'top',
          });
          this.actualityDeleted.emit(this.ActualityModel().id);
        }).catch(() => {
          this._snackBar.open("Erreur lors de la suppression de l'actualité", 'Fermer', {
            duration: 2000,
            horizontalPosition: 'end',
            verticalPosition: 'top',
          });
        });
      }
    });
  }

  openDialogUpdate(): void {
    const dialogRef = this.dialog.open(CreateUpdateActualityDialogComponent, {
      data: this.ActualityModel(),
      maxWidth: '90vw',
      width: 'fit-content',
      height: 'fit-content',
    });

    dialogRef.afterClosed().subscribe((result) => {
      // Fermer le dialogue par Échap ou en cliquant à côté renvoie `undefined`,
      // que `!== null` laissait passer : le parent recevait alors une entrée vide.
      if (result) {
        this.actualityUpdated.emit(result);
      }
    });
  }
}
