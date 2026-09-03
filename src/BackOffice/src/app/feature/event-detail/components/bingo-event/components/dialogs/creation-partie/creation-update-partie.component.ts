import {Component, inject, signal} from '@angular/core';
import {FormBuilder, FormGroup, Validators} from "@angular/forms";
import {MatSnackBar} from "@angular/material/snack-bar";
import {MAT_DIALOG_DATA, MatDialogRef} from "@angular/material/dialog";
import {PartieTypeEnum} from "../../../../../../../shared/enums/partieType.enum";
import {LotoFacadeService} from "../../../../../../../shared/facades/loto.facade.service";
import {VpdEventModel, VpdEventPartieModel} from "../../../../../../../shared/models/vpdEvent.model";
import {
  CreateUpdatePartieDataDialogInterface
} from "../../../../../../../shared/interfaces/createUpdatePartieDataDialog.interface";

@Component({
    selector: 'app-creation-partie',
    templateUrl: './creation-update-partie.component.html',
    standalone: false
})
export class CreationUpdatePartieComponent {
  newPartieForm: FormGroup;
  isLoading = signal(false);
  updatePartie = signal<VpdEventPartieModel | null>(null);
  protected readonly PartieTypeEnum = PartieTypeEnum;
  private readonly fb = inject(FormBuilder);
  private readonly lotoFacadeService = inject(LotoFacadeService);
  private readonly _snackBar = inject(MatSnackBar);
  private readonly _dialogRef = inject(MatDialogRef<CreationUpdatePartieComponent>)
  private readonly _data = inject<CreateUpdatePartieDataDialogInterface>(MAT_DIALOG_DATA);
  private readonly _event!: VpdEventModel;

  constructor() {
    this.updatePartie.set(this._data.partie);
    this._event = this._data.event;

    this.newPartieForm = this.fb.group({
      name: ['', Validators.required],
      partieType: [PartieTypeEnum.STANDARD, Validators.required],
      pauseAfter: [false, Validators.required],
    });

    if (this.updatePartie() !== null) {
      this.newPartieForm.get('name')?.setValue(this.updatePartie()!.name);
      this.newPartieForm.get('partieType')?.setValue(this.updatePartie()!.partieType);
      this.newPartieForm.get('pauseAfter')?.setValue(this.updatePartie()!.pauseAfter);
    }
  }

  onNoClick(): void {
    this._dialogRef.close(null);
  }

  onYesClick(): void {
    if (this.newPartieForm.invalid) {
      this.newPartieForm.markAllAsTouched();

      Object.keys(this.newPartieForm.controls).forEach(key => {
        const controlErrors = this.newPartieForm.get(key)!.errors;
        if (controlErrors) {
          console.log('Control Errors for:', key, controlErrors);
        }
      });
      return;
    }
    this.isLoading.set(true);
    if (this.updatePartie() === null) {
      this.lotoFacadeService.postCreatePartie$(this._event.id, this.createFormData()).then((result) => {
        this._snackBar.open("La partie a bien été créée", "Fermer", {
          duration: 2000,
          horizontalPosition: "end",
          verticalPosition: "top"
        });
        this.isLoading.set(false);
        console.log(result)
        this._dialogRef.close(result);
      }).catch((error) => {
        this._snackBar.open("Erreur lors de la création de cette partie", "Fermer", {
          duration: 2000,
          horizontalPosition: "end",
          verticalPosition: "top"
        });
        this.isLoading.set(false);
      })
    } else {
      this.lotoFacadeService.putUpdatePartie$(this._event.id, this.updatePartie()!.id, this.createFormData()).then((result) => {
        this._snackBar.open("La partie a bien été modifiée", "Fermer", {
          duration: 2000,
          horizontalPosition: "end",
          verticalPosition: "top"
        });
        this.isLoading.set(false);
        this._dialogRef.close(result);
      }).catch((error) => {
        this._snackBar.open("Erreur lors de la modification de cette partie", "Fermer", {
          duration: 2000,
          horizontalPosition: "end",
          verticalPosition: "top"
        });
        this.isLoading.set(false);
      })
    }
  }

  private createFormData() {
    const formData = new FormData();

    formData.append("Name", this.newPartieForm.get('name')?.value);
    formData.append("PartieType", this.newPartieForm.get('partieType')?.value);
    formData.append("PauseAfter", this.newPartieForm.get('pauseAfter')?.value);
    formData.append("Index", this.updatePartie() !== null ? this.updatePartie()!.index.toString() : this._event.parties.length.toString());

    return formData;
  }
}
