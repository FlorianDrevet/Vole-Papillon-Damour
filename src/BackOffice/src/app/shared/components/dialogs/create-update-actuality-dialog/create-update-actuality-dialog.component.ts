import {Component, inject, signal} from '@angular/core';
import {FormBuilder, FormGroup, Validators} from "@angular/forms";
import {ErrorsEnum} from "../../../enums/errors.enum";
import {MatSnackBar} from "@angular/material/snack-bar";
import {ActualityFacadeService} from "../../../facades/actuality.facade.service";
import {MAT_DIALOG_DATA, MatDialogRef} from "@angular/material/dialog";
import {FileUploadInterface} from "../../../interfaces/fileUpload.interface";
import {ActualityModel} from "../../../models/actuality.model";
import {ImageUtils} from "../../../utils/image.utils";

@Component({
    selector: 'app-create-update-actuality-dialog',
    templateUrl: './create-update-actuality-dialog.component.html',
    styleUrl: './create-update-actuality-dialog.component.scss',
    standalone: false
})
export class CreateUpdateActualityDialogComponent {
  newActualityForm: FormGroup;
  error = signal<ErrorsEnum | null>(null)
  modification = signal(false);
  isLoading = signal(false);
  hasValidationErrors = signal(false);
  principalImage = signal<FileUploadInterface>({fileName: '', fileContent: ''});
  optionalImages = signal<FileUploadInterface[]>([]);
  updateActuality = signal<ActualityModel | null>(null);


  private readonly fb = inject(FormBuilder);
  private readonly actualityFacade = inject(ActualityFacadeService);
  private readonly _snackBar = inject(MatSnackBar);
  private readonly _dialogRef = inject(MatDialogRef<CreateUpdateActualityDialogComponent>)
  private readonly _data = inject<ActualityModel | null>(MAT_DIALOG_DATA);

  constructor() {
    this.updateActuality.set(this._data);

    this.newActualityForm = this.fb.group({
      title: ['', Validators.required],
      principalImage: ['', Validators.required],
      date: ['', Validators.required],
      facebook: [null, Validators.pattern('https?://.+')],
      instagram: [null, Validators.pattern('https?://.+')],
      article: ['', Validators.required],
      images: ['', null],
    });

    if (this.updateActuality() !== null) {
      this.modification.set(true);
      this.newActualityForm.get('title')?.setValue(this.updateActuality()!.title);
      this.newActualityForm.get('date')?.setValue(this.updateActuality()!.date);
      this.newActualityForm.get('facebook')?.setValue(this.updateActuality()!.facebookLink ?? '');
      this.newActualityForm.get('instagram')?.setValue(this.updateActuality()!.instagramLink ?? '');
      this.newActualityForm.get('article')?.setValue(this.updateActuality()!.article);

      const fileName = this.updateActuality()!.urlPrincipalImage.split('/').pop();
      this.principalImage.set({fileName: fileName!, fileContent: new URL(this.updateActuality()!.urlPrincipalImage)});
      this.newActualityForm.get('principalImage')?.setValue(fileName);

      this.updateActuality()!.images.forEach((url) => {
        const fileName = url.split('/').pop();
        this.optionalImages.update(x => [...x, {fileName: fileName!, fileContent: new URL(url)}]);
      });
    }
  }

  /**
   * Les champs fichier ne sont pas reliés au formulaire (voir le gabarit) : c'est
   * ici qu'on répercute la sélection sur l'aperçu et sur le contrôle
   * `principalImage`, qui ne sert plus qu'à porter la validation.
   */
  onPrincipalImageSelected(input: HTMLInputElement): void {
    const file = input.files?.[0];
    if (!file) {
      return;
    }

    ImageUtils.onFileSelected(input, this.principalImage);

    const control = this.newActualityForm.get('principalImage');
    control?.setValue(file.name);
    control?.markAsDirty();
  }

  onOptionalImagesSelected(input: HTMLInputElement): void {
    if (!input.files?.length) {
      return;
    }

    ImageUtils.onFilesSelected(input, this.optionalImages);

    // Le champ est vidé pour qu'une deuxième sélection du même fichier déclenche
    // bien un nouvel évènement `change`.
    input.value = '';
  }

  onNoClick(): void {
    this._dialogRef.close(null);
  }

  onYesClick(): void {
    if (this.newActualityForm.invalid) {
      // Le bouton ne faisait rien et les erreurs de saisie partaient dans la
      // console : de l'extérieur, la modale paraissait bloquée.
      this.newActualityForm.markAllAsTouched();
      this.hasValidationErrors.set(true);
      return;
    }

    this.hasValidationErrors.set(false);
    this.isLoading.set(true);
    if (this.updateActuality() === null) {
      this.actualityFacade.postNewActuality$(this.createFormData()).then((result) => {
        this._snackBar.open("L'actualité a bien été créée", "Fermer", {
          duration: 2000,
          horizontalPosition: "end",
          verticalPosition: "top"
        });
        this.isLoading.set(false);
        this._dialogRef.close(result);
      }).catch((error) => {
        this._snackBar.open("Erreur lors de la création de cette actualité", "Fermer", {
          duration: 2000,
          horizontalPosition: "end",
          verticalPosition: "top"
        });
        this.isLoading.set(false);
      })
    } else {
      this.actualityFacade.putUpdateActuality$(this.updateActuality()!.id, this.createFormData()).then((result) => {
        this._snackBar.open("L'actualité a bien été modifiée", "Fermer", {
          duration: 2000,
          horizontalPosition: "end",
          verticalPosition: "top"
        });
        this.isLoading.set(false);
        this._dialogRef.close(result);
      }).catch((error) => {
        this._snackBar.open("Erreur lors de la modification de cette actualité", "Fermer", {
          duration: 2000,
          horizontalPosition: "end",
          verticalPosition: "top"
        });
        this.isLoading.set(false);
      })
    }
  }

  deleteOptionalImage(index: number) {
    this.optionalImages.update(x => x.filter((_, i) => i !== index));
  }

  private createFormData() {
    const formData = new FormData();

    const optionalImagesFiles = this.optionalImages().filter(image => typeof image.fileContent === 'string');
    optionalImagesFiles.forEach((image, index) => {
      formData.append(`Images[${index}]`, ImageUtils.createBlobFromImage(image.fileContent as string), image.fileName);
    });

    const optionalImagesUrls = this.optionalImages().filter(image => typeof image.fileContent !== 'string');
    optionalImagesUrls.forEach((image, index) => {
      formData.append(`ImagesUrls[${index}]`, image.fileContent as string);
    });

    if (typeof this.principalImage().fileContent !== 'string') {
      formData.append("PrincipalImageUri", this.principalImage().fileContent as string);
    } else {
      formData.append("PrincipalImage", ImageUtils.createBlobFromImage(this.principalImage().fileContent as string), this.principalImage().fileName);
    }
    formData.append("Title", this.newActualityForm.get('title')?.value);
    const dateActuality = new Date(this.newActualityForm.get('date')?.value!);
    formData.append("Date", (new Date(Date.UTC(dateActuality.getFullYear(), dateActuality.getMonth(), dateActuality.getDate(), 0, 0, 0))).toISOString());
    formData.append("Article", this.newActualityForm.get('article')?.value);

    if (this.newActualityForm.get('facebook')?.value !== null) {
      formData.append("FacebookLink", this.newActualityForm.get('facebook')?.value);
    }
    if (this.newActualityForm.get('instagram')?.value !== null) {
      formData.append("InstagramLink", this.newActualityForm.get('instagram')?.value);
    }

    return formData;
  }

}
