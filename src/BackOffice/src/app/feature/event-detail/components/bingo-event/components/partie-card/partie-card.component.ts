import {Component, computed, inject, input, output} from '@angular/core';
import {VpdEventModel, VpdEventPartieModel} from "../../../../../../shared/models/vpdEvent.model";
import {MatDialog} from "@angular/material/dialog";
import {CreationUpdatePartieComponent} from "../dialogs/creation-partie/creation-update-partie.component";
import {
  CreateUpdatePartieDataDialogInterface
} from "../../../../../../shared/interfaces/createUpdatePartieDataDialog.interface";
import {
  ConfirmationDialogComponent
} from "../../../../../../shared/components/dialogs/confirmation-dialog/confirmation-dialog.component";
import {LotoFacadeService} from "../../../../../../shared/facades/loto.facade.service";
import {MatSnackBar} from "@angular/material/snack-bar";
import {compareNumberLines, NumberLineEnum} from "../../../../../../shared/enums/numberLine.enum";
import {CreationUpdateLotComponent} from "../dialogs/creation-update-line-partie/creation-update-lot.component";
import {
  CreateUpdateLinePartieDataDialogInterface
} from "../../../../../../shared/interfaces/createUpdateLinePartieDataDialog.interface";
import {PartieTypeEnum} from "../../../../../../shared/enums/partieType.enum";

@Component({
    selector: 'app-partie-card',
    templateUrl: './partie-card.component.html',
    styleUrl: './partie-card.component.scss',
    standalone: false
})
export class PartieCardComponent {
  Partie = input.required<VpdEventPartieModel>()
  oneLineLots = computed(() => this.Partie().lineParties.filter(linePartie => linePartie.numberLine === NumberLineEnum.ONELINE && linePartie.lots.length > 0));
  twoLineLots = computed(() => this.Partie().lineParties.filter(linePartie => linePartie.numberLine === NumberLineEnum.TWOLINE && linePartie.lots.length > 0));
  cartonPleinLots = computed(() => this.Partie().lineParties.filter(linePartie => linePartie.numberLine === NumberLineEnum.CARTONPLEIN && linePartie.lots.length > 0));
  lineParties = computed(() => {
    const l = this.Partie().lineParties;
    l.sort((a, b) => compareNumberLines(a.numberLine, b.numberLine));
    return l.filter(linePartie => linePartie.lots.length > 0);
  });
  Event = input.required<VpdEventModel>()

  readonly dialog = inject(MatDialog);
  eventUpdated = output<VpdEventModel>()
  protected readonly NumberLineEnum = NumberLineEnum;
  private readonly _lotoFacade = inject(LotoFacadeService);
  private readonly _snackBar = inject(MatSnackBar);

  openDialogDeletion() {
    const dialogRef = this.dialog.open(ConfirmationDialogComponent, {
      data: {title: "Êtes-vous sûr de vouloir supprimer cette partie ?"},
    });

    dialogRef.afterClosed().subscribe(result => {
      if (result) {
        this._lotoFacade.deletePartie$(this.Event().id, this.Partie().id).then(() => {
          this._snackBar.open("Partie supprimée avec succès", 'Fermer', {
            duration: 2000,
            horizontalPosition: 'end',
            verticalPosition: 'top',
          });
          this.Event().parties = this.Event().parties.filter(partie => partie.id !== this.Partie().id);
          this.eventUpdated.emit(this.Event());
        }).catch(() => {
          this._snackBar.open("Erreur lors de la suppression de la partie", 'Fermer', {
            duration: 2000,
            horizontalPosition: 'end',
            verticalPosition: 'top',
          });
        });
      }
    });
  }

  openDialogUpdate() {
    const dialogRef = this.dialog.open(CreationUpdatePartieComponent, {
      data: {
        partie: this.Partie(),
        event: this.Event(),
      } as CreateUpdatePartieDataDialogInterface,

      "maxWidth": "90vw",
      "width": "fit-content",
      "height": "fit-content",
    });

    dialogRef.afterClosed().subscribe((result: VpdEventModel) => {
      if (result !== null) {
        this.eventUpdated.emit(result);
      }
    });
  }

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
        this.eventUpdated.emit(result);
      }
    });
  }

  lotUpdated($event: VpdEventModel) {
    this.eventUpdated.emit($event);
  }

  protected readonly PartieTypeEnum = PartieTypeEnum;
}
