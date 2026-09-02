import {Component, inject} from '@angular/core';
import {MsalService} from '@azure/msal-angular';
import {MatSnackBar} from "@angular/material/snack-bar";

import {loginRequest} from '../../shared/auth/msal-config';

@Component({
    selector: 'app-login',
    templateUrl: './login.component.html',
    styleUrl: './login.component.scss',
    standalone: false
})
export class LoginComponent {
  private readonly msalService = inject(MsalService);
  private readonly snackBar = inject(MatSnackBar);

  public onLoginClick(): void {
    this.msalService.loginRedirect(loginRequest).subscribe({
      error: () => this.snackBar.open('Erreur lors de la connexion à Microsoft.', 'Fermer', {
        horizontalPosition: 'end',
        verticalPosition: 'top',
      }),
    });
  }
}
