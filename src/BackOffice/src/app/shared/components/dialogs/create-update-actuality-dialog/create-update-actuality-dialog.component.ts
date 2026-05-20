import {Component, inject, signal, WritableSignal} from '@angular/core';
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

  onNoClick(): void {
    this._dialogRef.close(null);
  }

  onYesClick(): void {
    if (this.newActualityForm.invalid) {
      this.newActualityForm.markAllAsTouched();

      Object.keys(this.newActualityForm.controls).forEach(key => {
        const controlErrors = this.newActualityForm.get(key)!.errors;
        if (controlErrors) {
          console.log('Control Errors for:', key, controlErrors);
        }
      });
      return;
    }
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

  onFileSelected(id: string,  upload: WritableSignal<any>) {
    const inputNode: any = document.querySelector(id);

    if (typeof (FileReader) !== 'undefined') {
      const reader = new FileReader();

      reader.onload = (e: any) => {
        this.principalImage.set({fileName: inputNode.files[0].name, fileContent: e.target!.result});
      };

      reader.readAsDataURL(inputNode.files[0]);
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

  protected readonly ImageUtils = ImageUtils;
  protected readonly document = document;
}
