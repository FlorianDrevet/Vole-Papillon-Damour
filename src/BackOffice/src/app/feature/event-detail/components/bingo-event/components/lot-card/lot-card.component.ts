import {Component, computed, inject, input, output, signal} from '@angular/core';
import {
  VpdEventModel,
  VpdEventPartieLineModel,
  VpdEventPartieModel
} from "../../../../../../shared/models/vpdEvent.model";
import {LotoFacadeService} from "../../../../../../shared/facades/loto.facade.service";
import {MatDialog} from "@angular/material/dialog";
import {MatSnackBar} from "@angular/material/snack-bar";
import {
  ConfirmationDialogComponent
} from "../../../../../../shared/components/dialogs/confirmation-dialog/confirmation-dialog.component";
import {CreationUpdateLotComponent} from "../dialogs/creation-update-line-partie/creation-update-lot.component";
import {
  CreateUpdateLinePartieDataDialogInterface
} from "../../../../../../shared/interfaces/createUpdateLinePartieDataDialog.interface";
import {NumberLineEnum} from "../../../../../../shared/enums/numberLine.enum";
import {PartieTypeEnum} from "../../../../../../shared/enums/partieType.enum";

@Component({
    selector: 'app-lot-card',
    templateUrl: './lot-card.component.html',
    standalone: false
})
export class LotCardComponent {
  LinePartie = input.required<VpdEventPartieLineModel>()
  Event = input.required<VpdEventModel>()
  Partie = input.required<VpdEventPartieModel>()
  readonly dialog = inject(MatDialog);
  lotUpdated = output<VpdEventModel>()
  private _currentLotIndex = signal(0);
  currentLot = computed(() => this.LinePartie().lots[this._currentLotIndex()])
  private readonly _snackBar = inject(MatSnackBar);
  private readonly _lotoFacade = inject(LotoFacadeService);

  nextLot() {
    this._currentLotIndex.update((index) => {
      if (index === this.LinePartie().lots.length - 1)
        return 0;
      return index + 1
    })
  }

  previousLot() {
    this._currentLotIndex.update((index) => {
      if (index === 0)
        return this.LinePartie().lots.length - 1;
      return index - 1
    });
  }

  openDialogUpdate(numberOfLine: NumberLineEnum) {
    const dialogRef = this.dialog.open(CreationUpdateLotComponent, {
      data: {
        partie: this.Partie(),
        event: this.Event(),
        numberLine: numberOfLine,
        linePartie: this.LinePartie(),
        lot: this.currentLot(),
      } as CreateUpdateLinePartieDataDialogInterface,

      "maxWidth": "90vw",
      "width": "fit-content",
      "height": "fit-content",
    });

    dialogRef.afterClosed().subscribe((result: VpdEventModel) => {
      if (result !== null) {
        this.lotUpdated.emit(result);
      }
    });
  }


  openDialogDeletion() {
    const dialogRef = this.dialog.open(ConfirmationDialogComponent, {
      data: {title: "Êtes-vous sûr de vouloir supprimer cet Lot ?"},
    });

    dialogRef.afterClosed().subscribe(result => {
      if (result) {
        this._lotoFacade.deleteLot$(this.Event().id, this.Partie().id, this.LinePartie().id, this.currentLot().id).then((result) => {
          this._snackBar.open("Lot supprimée avec succès", 'Fermer', {
            duration: 2000,
            horizontalPosition: 'end',
            verticalPosition: 'top',
          });
          this.lotUpdated.emit(result);
        }).catch(() => {
          this._snackBar.open("Erreur lors de la suppression du lot", 'Fermer', {
            duration: 2000,
            horizontalPosition: 'end',
            verticalPosition: 'top',
          });
        });
      }
    });
  }

  protected readonly PartieTypeEnum = PartieTypeEnum;

  openDialogAddPartieLine(numberOfLine: NumberLineEnum) {
    const dialogRef = this.dialog.open(CreationUpdateLotComponent, {
      data: {
        partie: this.Partie(),
        event: this.Event(),
        numberLine: numberOfLine,
        linePartie: null,
        lot: null,
      } as CreateUpdateLinePartieDataDialogInterface,

      "maxWidth": "90vw",
      "width": "fit-content",
      "height": "fit-content",
    });

    dialogRef.afterClosed().subscribe((result: VpdEventModel) => {
      if (result !== null) {
        this.lotUpdated.emit(result);
      }
    });
  }
}
