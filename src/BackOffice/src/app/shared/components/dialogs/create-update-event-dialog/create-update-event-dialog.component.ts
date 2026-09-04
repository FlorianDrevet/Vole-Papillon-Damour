import {Component, inject, OnInit, signal} from '@angular/core';
import {FormBuilder, FormGroup, Validators} from "@angular/forms";
import {ErrorsEnum} from "../../../enums/errors.enum";
import {FileUploadInterface} from "../../../interfaces/fileUpload.interface";
import {MatSnackBar} from "@angular/material/snack-bar";
import {MAT_DIALOG_DATA, MatDialogRef} from "@angular/material/dialog";
import {VpdEventModel} from "../../../models/vpdEvent.model";
import {VpdEventsFacadeService} from "../../../facades/vpd-events.facade.service";
import {ImageUtils} from "../../../utils/image.utils";
import {VpdEventEnum} from "../../../enums/vpdEvent.enum";
import {fromApiUtcDate, fromApiUtcWallClock, MyDate} from "../../../extensions/MyDate";

@Component({
    selector: 'app-create-update-event-dialog',
    templateUrl: './create-update-event-dialog.component.html',
    styleUrl: './create-update-event-dialog.component.scss',
    standalone: false
})
export class CreateUpdateEventDialogComponent implements OnInit {
  newEventForm: FormGroup;
  error = signal<ErrorsEnum | null>(null)
  isLoading = signal(false);
  hasValidationErrors = signal(false);
  principalImage = signal<FileUploadInterface>({fileName: '', fileContent: ''});
  updateEvent = signal<VpdEventModel | null>(null);

  type = signal(VpdEventEnum.Bingo)

  private readonly fb = inject(FormBuilder);
  private readonly _eventFacade = inject(VpdEventsFacadeService);
  private readonly _snackBar = inject(MatSnackBar);
  private readonly _dialogRef = inject(MatDialogRef<CreateUpdateEventDialogComponent>)
  private readonly _data = inject<VpdEventModel | null>(MAT_DIALOG_DATA);

  constructor() {
    this.updateEvent.set(this._data);

    this.newEventForm = this.fb.group({
      eventType: [VpdEventEnum.Bingo, Validators.required],
      name: ['', Validators.required],
      description: ['', Validators.required],
      dateStart: ['', Validators.required],
      hourStart: ['', null],
      dateEnd: ['', null],
      hourOpenDoors: ['', null],
      hourCloseDoors: ['', null],
      urlRegistration: ['', null],
      image: ['', Validators.required],
      city: ['', Validators.required],
      road: ['', Validators.required],
      cityCode: ['', Validators.required],
      roadNumber: ['', Validators.required],
    });

    if (this.updateEvent() !== null) {
      this.type.set(this.updateEvent()!.eventType);
      this.newEventForm.get('eventType')?.setValue(this.updateEvent()!.eventType);
      this.newEventForm.get('name')?.setValue(this.updateEvent()!.name);
      this.newEventForm.get('description')?.setValue(this.updateEvent()!.description);
      this.newEventForm.get('dateStart')?.setValue(fromApiUtcDate(this.updateEvent()!.dateStart));
      this.newEventForm.get('hourStart')?.setValue(fromApiUtcWallClock(this.updateEvent()!.dateStart));
      this.newEventForm.get('dateEnd')?.setValue(fromApiUtcDate(this.updateEvent()!.dateEnd));
      this.newEventForm.get('hourOpenDoors')?.setValue(fromApiUtcWallClock(this.updateEvent()!.hourOpenDoors));
      this.newEventForm.get('hourCloseDoors')?.setValue(fromApiUtcWallClock(this.updateEvent()!.hourCloseDoors));
      this.newEventForm.get('urlRegistration')?.setValue(this.updateEvent()!.urlRegistration);
      this.newEventForm.get('city')?.setValue(this.updateEvent()!.city);
      this.newEventForm.get('road')?.setValue(this.updateEvent()!.road);
      this.newEventForm.get('cityCode')?.setValue(this.updateEvent()!.cityCode);
      this.newEventForm.get('roadNumber')?.setValue(this.updateEvent()!.roadNumber);

      const fileName = this.updateEvent()!.urlImage.split('/').pop();
      this.principalImage.set({fileName: fileName!, fileContent: new URL(this.updateEvent()!.urlImage)});
      this.newEventForm.get('image')?.setValue(fileName);
    }
  }

  ngOnInit(): void {
    this.newEventForm.get('eventType')?.valueChanges!.subscribe((value) => {
      this.type.set(value);
      this._changeValidators(value);
    })
  }

  private _changeValidators(value: VpdEventEnum) {
    if (value === VpdEventEnum.Bingo || value === VpdEventEnum.Other) {
      this.newEventForm.get('hourStart')?.setValidators([Validators.required]);
    } else {
      this.newEventForm.get('hourStart')?.clearValidators();
    }

    if (value === VpdEventEnum.Other) {
      this.newEventForm.get('hourOpenDoors')?.clearValidators();
    } else {
      this.newEventForm.get('hourOpenDoors')?.setValidators([Validators.required]);
    }
  }

  /**
   * Le champ fichier n'est pas relié au formulaire (voir le gabarit) : c'est ici
   * qu'on répercute la sélection sur l'aperçu et sur le contrôle `image`, qui ne
   * sert plus qu'à porter la validation « une image est obligatoire ».
   */
  onImageSelected(input: HTMLInputElement): void {
    const file = input.files?.[0];
    if (!file) {
      return;
    }

    ImageUtils.onFileSelected(input, this.principalImage);

    const control = this.newEventForm.get('image');
    control?.setValue(file.name);
    control?.markAsDirty();
  }

  onNoClick(): void {
    this._dialogRef.close(null);
  }

  onYesClick(): void {
    if (this.newEventForm.invalid) {
      // Le bouton ne faisait rien et les erreurs de saisie partaient dans la
      // console : de l'extérieur, la modale paraissait bloquée.
      this.newEventForm.markAllAsTouched();
      this.hasValidationErrors.set(true);
      return;
    }

    this.hasValidationErrors.set(false);
    this.isLoading.set(true);
    if (this.updateEvent() === null) {
      this._eventFacade.postNewEvent$(this.createFormData()).then((result) => {
        this._snackBar.open("L'évènement a bien été créée", "Fermer", {
          duration: 2000,
          horizontalPosition: "end",
          verticalPosition: "top"
        });
        this.isLoading.set(false);
        this._dialogRef.close(result);
      }).catch((error) => {
        this._snackBar.open("Erreur lors de la création de cette évènement", "Fermer", {
          duration: 2000,
          horizontalPosition: "end",
          verticalPosition: "top"
        });
        this.isLoading.set(false);
      })
    } else {
      this._eventFacade.putUpdateEvent$(this.updateEvent()!.id, this.createFormData()).then((result) => {
        this._snackBar.open("L'évènement a bien été modifiée", "Fermer", {
          duration: 2000,
          horizontalPosition: "end",
          verticalPosition: "top"
        });
        this.isLoading.set(false);
        this._dialogRef.close(result);
      }).catch((error) => {
        this._snackBar.open("Erreur lors de la modification de cet évènement", "Fermer", {
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

    if (typeof this.principalImage().fileContent !== 'string') {
      formData.append("ImageUri", this.principalImage().fileContent as string);
    } else {
      formData.append("Image", ImageUtils.createBlobFromImage(this.principalImage().fileContent as string), this.principalImage().fileName);
    }

    formData.append("EventType", this.newEventForm.get('eventType')?.value);
    formData.append("Name", this.newEventForm.get('name')?.value);
    formData.append("Description", this.newEventForm.get('description')?.value);
    formData.append("City", this.newEventForm.get('city')?.value);
    formData.append("Road", this.newEventForm.get('road')?.value);
    formData.append("CityCode", this.newEventForm.get('cityCode')?.value);
    formData.append("RoadNumber", this.newEventForm.get('roadNumber')?.value);

    if (this.type() === VpdEventEnum.Books) {
      const dateStart = new MyDate(this.newEventForm.get('dateStart')?.value);
      formData.append("DateStart", dateStart.toISOUtcString());
    } else {
      const dateStart = new Date(this.newEventForm.get('dateStart')?.value);
      const hourStart = new Date(this.newEventForm.get('hourStart')?.value);
      const utcDate = new MyDate(
        dateStart.getFullYear(),
        dateStart.getMonth(),
        dateStart.getDate(),
        hourStart.getHours(),
        hourStart.getMinutes()
      );
      formData.append("DateStart", utcDate.toISOUtcString());
    }

    const hourOpenDoors = new MyDate(this.newEventForm.get('hourOpenDoors')?.value);
    switch (this.type()) {
      case VpdEventEnum.Bingo:
        formData.append("HourOpenDoors", hourOpenDoors.toISOUtcString());
        break;
      case VpdEventEnum.Books:
        const hourCloseDoors = new MyDate(this.newEventForm.get('hourCloseDoors')?.value);
        const dateEnd = new MyDate(this.newEventForm.get('dateEnd')?.value);
        formData.append("HourOpenDoors", hourOpenDoors.toISOUtcString());
        formData.append("HourCloseDoors", hourCloseDoors.toISOUtcString());
        formData.append("DateEnd", dateEnd.toISOUtcString());
        break;
      case VpdEventEnum.Other:
        if (this.newEventForm.get('hourOpenDoors')?.value !== null) {
          formData.append("UrlRegistration", this.newEventForm.get('UrlRegistration')?.value);
        }
        break;
    }

    console.log(formData);
    return formData;
  }

  protected readonly VpdEventEnum = VpdEventEnum;
}
