import {AfterViewInit, Component, ElementRef, inject, input, output, Renderer2, ViewChild} from '@angular/core';
import {ActualityModel} from "../../models/actuality.model";
import {MatDialog} from "@angular/material/dialog";
import {ConfirmationDialogComponent} from "../dialogs/confirmation-dialog/confirmation-dialog.component";
import {
  CreateUpdateActualityDialogComponent
} from "../dialogs/create-update-actuality-dialog/create-update-actuality-dialog.component";
import {ActualityFacadeService} from "../../facades/actuality.facade.service";
import {MatSnackBar} from "@angular/material/snack-bar";

@Component({
    selector: 'app-actuality-card',
    templateUrl: './actuality-card.component.html',
    styleUrl: './actuality-card.component.scss',
    standalone: false
})
export class ActualityCardComponent implements AfterViewInit {
  actualityDeleted = output<string>()
  actualityUpdated = output<ActualityModel>()
  ActualityModel = input.required<ActualityModel>()

  readonly dialog = inject(MatDialog);
  readonly actualityFacade = inject(ActualityFacadeService);
  private _snackBar = inject(MatSnackBar);

  openDialogDeletion(): void {
    const dialogRef = this.dialog.open(ConfirmationDialogComponent, {
      data: {title: "Êtes-vous sûr de vouloir supprimer cette actualité ?"},
    });

    dialogRef.afterClosed().subscribe(result => {
      if (result) {
        this.actualityFacade.deleteActualityById$(this.ActualityModel().id).then(() => {
          this._snackBar.open("Actualité supprimée avec succès", 'Fermer', {
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
      "maxWidth": "90vw",
      "width": "fit-content",
      "height": "fit-content",
    });

    dialogRef.afterClosed().subscribe(result => {
      if (result !== null) {
        this.actualityUpdated.emit(result);
      }
    });
  }

  @ViewChild('article') article!: ElementRef;
  private readonly renderer = inject(Renderer2);

  ngAfterViewInit() {
    const divElement = this.article.nativeElement;
    const divHeight = divElement.offsetHeight;
    const fontSize = parseFloat(window.getComputedStyle(divElement).fontSize);
    const lineHeight = parseFloat(window.getComputedStyle(divElement).lineHeight);

    const actualLineHeight = isNaN(lineHeight) ? fontSize * 1.2 : lineHeight;

    const numberOfLines = Math.floor(divHeight / actualLineHeight);

    this.renderer.setStyle(divElement, '-webkit-line-clamp', numberOfLines.toString());
  }
}
