import {Component, inject, signal} from '@angular/core';
import {MAT_DIALOG_DATA, MatDialogRef} from "@angular/material/dialog";
import {MailingFacadeService} from "../../../facades/mailing.facade.service";
import {FormBuilder, FormGroup, Validators} from "@angular/forms";
import {MatSnackBar} from "@angular/material/snack-bar";

@Component({
    selector: 'app-add-email-dialog',
    templateUrl: './add-email-dialog.component.html',
    styleUrl: './add-email-dialog.component.scss',
    standalone: false
})
export class AddEmailDialogComponent {
  readonly dialogRef = inject(MatDialogRef<AddEmailDialogComponent>);
  readonly mailService = inject(MailingFacadeService);

  emailForm: FormGroup;

  private readonly fb = inject(FormBuilder);
  private readonly _snackBar = inject(MatSnackBar);

  isLoading = signal(false);

  constructor() {
    this.emailForm = this.fb.group({
      email: ['', Validators.required]
    });
  }

  onNoClick(): void {
    this.dialogRef.close(false);
  }

  onYesClick(): void {
    if (this.emailForm.invalid) {
      this.emailForm.markAllAsTouched();

      Object.keys(this.emailForm.controls).forEach(key => {
        const controlErrors = this.emailForm.get(key)!.errors;
        if (controlErrors) {
          console.log('Control Errors for:', key, controlErrors);
        }
      });
      return;
    }

    this.isLoading.set(true);

    this.mailService.postAddEmail(this.emailForm.get('email')?.value).then(() => {
      this.isLoading.set(false);
      this._snackBar.open('Email ajouté avec succès', 'Fermer', {
        duration: 2000,
      });
    }).catch(() => {
      this.isLoading.set(false);
      this._snackBar.open('Erreur lors de l\'ajout de l\'email', 'Fermer', {
        duration: 2000,
      });
    });
    this.dialogRef.close(true);
  }
}
