import {Component, inject, signal} from '@angular/core';
import {FormBuilder, FormGroup, Validators} from "@angular/forms";
import {
  VpdEventModel,
  VpdEventPartieLineModel,
  VpdEventPartieLotModel,
  VpdEventPartieModel
} from "../../../../../../../shared/models/vpdEvent.model";
import {LotoFacadeService} from "../../../../../../../shared/facades/loto.facade.service";
import {MatSnackBar} from "@angular/material/snack-bar";
import {MAT_DIALOG_DATA, MatDialogRef} from "@angular/material/dialog";
import {CreationUpdatePartieComponent} from "../creation-partie/creation-update-partie.component";
import {PartieTypeEnum} from "../../../../../../../shared/enums/partieType.enum";
import {
  CreateUpdateLinePartieDataDialogInterface
} from "../../../../../../../shared/interfaces/createUpdateLinePartieDataDialog.interface";
import {NumberLineEnum} from "../../../../../../../shared/enums/numberLine.enum";
import {FileUploadInterface} from "../../../../../../../shared/interfaces/fileUpload.interface";
import {ImageUtils} from "../../../../../../../shared/utils/image.utils";

@Component({
    selector: 'app-creation-update-line-partie',
    templateUrl: './creation-update-lot.component.html',
    styleUrl: './creation-update-lot.component.scss',
    standalone: false
})
export class CreationUpdateLotComponent {
  newLotForm: FormGroup;
  isLoading = signal(false);
  updateLot = signal<VpdEventPartieLotModel | null>(null);
  principalImage = signal<FileUploadInterface>({fileName: '', fileContent: ''});
  protected readonly PartieTypeEnum = PartieTypeEnum;
  protected readonly document = document;
  protected readonly ImageUtils = ImageUtils;
  private readonly fb = inject(FormBuilder);
  private readonly lotoFacadeService = inject(LotoFacadeService);
  private readonly _snackBar = inject(MatSnackBar);
  private readonly _dialogRef = inject(MatDialogRef<CreationUpdatePartieComponent>)
  private readonly _data = inject<CreateUpdateLinePartieDataDialogInterface>(MAT_DIALOG_DATA);
  private readonly _event!: VpdEventModel;
  private readonly _partie!: VpdEventPartieModel;
  private readonly _partieLine: VpdEventPartieLineModel | null;
  private readonly _numberLine!: NumberLineEnum;

  constructor() {
    this.updateLot.set(this._data.lot);

    this._event = this._data.event;
    this._partie = this._data.partie;
    this._partieLine = this._data.linePartie;
    this._numberLine = this._data.numberLine;

    this.newLotForm = this.fb.group({
      name: ['', Validators.required],
      image: [null, Validators.required],
    });

    if (this.updateLot() !== null) {
      this.newLotForm.get('name')?.setValue(this.updateLot()!.name);

      const fileName = this.updateLot()!.urlImage.split('/').pop();
      this.principalImage.set({fileName: fileName!, fileContent: new URL(this.updateLot()!.urlImage)});
      this.newLotForm.get('image')?.setValue(fileName);
    }
  }

  onNoClick(): void {
    this._dialogRef.close(null);
  }

  onYesClick(): void {
    if (this.newLotForm.invalid) {
      this.newLotForm.markAllAsTouched();

      Object.keys(this.newLotForm.controls).forEach(key => {
        const controlErrors = this.newLotForm.get(key)!.errors;
        if (controlErrors) {
          console.log('Control Errors for:', key, controlErrors);
        }
      });
      return;
    }
    this.isLoading.set(true);
    if (this.updateLot() === null) {
      this.lotoFacadeService.postCreateLot$(this._event, this._partie.id, this._numberLine, this.createFormData()).then((result) => {
        this._snackBar.open("Le lot a bien été créée", "Fermer", {
          duration: 2000,
          horizontalPosition: "end",
          verticalPosition: "top"
        });
        this.isLoading.set(false);
        console.log(result)
        this._dialogRef.close(result);
      }).catch((error) => {
        this._snackBar.open("Erreur lors de la création du lot", "Fermer", {
          duration: 2000,
          horizontalPosition: "end",
          verticalPosition: "top"
        });
        this.isLoading.set(false);
      })
    } else {
      this.lotoFacadeService.putUpdateLot$(this._event.id, this._partie.id, this._partieLine!.id, this.updateLot()!.id, this.createFormData()).then((result) => {
        this._snackBar.open("Le lot a bien été modifiée", "Fermer", {
          duration: 2000,
          horizontalPosition: "end",
          verticalPosition: "top"
        });
        this.isLoading.set(false);
        this._dialogRef.close(result);
      }).catch((error) => {
        this._snackBar.open("Erreur lors de la modification de ce lot", "Fermer", {
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

    formData.append("Name", this.newLotForm.get('name')?.value);
    formData.append("Index", this.updateLot() !== null ? this.updateLot()!.index.toString() : "0");

    if (typeof this.principalImage().fileContent !== 'string') {
      formData.append("ImageUri", this.principalImage().fileContent as string);
    } else {
      formData.append("Image", ImageUtils.createBlobFromImage(this.principalImage().fileContent as string), this.principalImage().fileName);
    }

    return formData;
  }
}
