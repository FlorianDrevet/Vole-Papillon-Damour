import {Component, inject, OnInit, signal} from '@angular/core';
import {FormBuilder, FormGroup, Validators} from "@angular/forms";
import {ErrorsEnum} from "../../../enums/errors.enum";
import {FileUploadInterface} from "../../../interfaces/fileUpload.interface";
import {MatDialogRef} from "@angular/material/dialog";
import {ImageUtils} from "../../../utils/image.utils";
import {VpdEventEnum} from "../../../enums/vpdEvent.enum";
import {BingoCardFacadeService} from "../../../facades/bingo-card.facade.service";
import {BingoCardInterface} from "../../../interfaces/bingoCard.interface";

@Component({
  selector: 'app-create-update-event-dialog',
  templateUrl: './scan-bingo-card-dialog.component.html',
  styleUrl: './scan-bingo-card-dialog.component.scss'
})
export class ScanBingoCardDialogComponent implements OnInit {
  newEventForm: FormGroup;
  error = signal<ErrorsEnum | null>(null)
  isLoading = signal(false);
  bingoCardImage = signal<FileUploadInterface>({fileName: '', fileContent: ''});
  public bingoCards = signal<BingoCardInterface[]>([]);
  // bingo card use to test init at the beginning
  public testBingoCard = {
    firstLine: [1, 2, 3, 4, 5],
    secondLine: [6, 7, 8, 9, 10],
    thirdLine: [11, 12, 13, 14, 15],
  }
  protected readonly VpdEventEnum = VpdEventEnum;
  protected readonly ImageUtils = ImageUtils;
  protected readonly document = document;
  private readonly fb = inject(FormBuilder);
  private readonly _bingoCardFacade = inject(BingoCardFacadeService);
  private readonly _dialogRef = inject(MatDialogRef<ScanBingoCardDialogComponent>)

  constructor() {

    this.newEventForm = this.fb.group({
      image: ['', Validators.required],
    });
  }

  ngOnInit(): void {
  }

  onNoClick(): void {
    this._dialogRef.close(null);
  }

  onYesClick(): void {
    if (this.newEventForm.invalid) {
      this.newEventForm.markAllAsTouched();

      Object.keys(this.newEventForm.controls).forEach(key => {
        const controlErrors = this.newEventForm.get(key)!.errors;
        if (controlErrors) {
          console.log('Control Errors for:', key, controlErrors);
        }
      });
      return;
    }
    this.isLoading.set(true);
    this._bingoCardFacade.postBingoCardAnalyze$(this.createFormData()).then((result) => {
      console.log(result);
      this.bingoCards.set(result);
      this.isLoading.set(false);
    }).catch((error) => {
      this.isLoading.set(false);
    })
  }

  private createFormData() {
    const formData = new FormData();

    formData.append("Image", ImageUtils.createBlobFromImage(this.bingoCardImage().fileContent as string), this.bingoCardImage().fileName);
    formData.append("EventId", "8fc4432a-88f6-4a02-be1e-5ed360dfad07"); //TODO

    return formData;
  }
}
