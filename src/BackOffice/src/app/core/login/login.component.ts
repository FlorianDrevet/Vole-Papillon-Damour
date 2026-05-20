import {Component, inject, signal} from '@angular/core';
import {FormBuilder, FormGroup, Validators} from "@angular/forms";
import {Router} from "@angular/router";
import {ErrorsEnum} from "../../shared/enums/errors.enum";
import {AuthenticationService} from "../../shared/services/authentication.service";
import {AuthenticationFacadeService} from "../../shared/facades/authentication.facade.service";
import {MatSnackBar} from "@angular/material/snack-bar";

@Component({
    selector: 'app-login',
    templateUrl: './login.component.html',
    styleUrl: './login.component.scss',
    standalone: false
})
export class LoginComponent {
  loginForm: FormGroup;
  error = signal<ErrorsEnum | null>(null)

  private fb = inject(FormBuilder);
  private authFacade = inject(AuthenticationFacadeService);
  private authService = inject(AuthenticationService);
  private router = inject(Router);
  private _snackBar = inject(MatSnackBar);

  constructor() {
    this.loginForm = this.fb.group({
      username: ['', Validators.required],
      password: ['', Validators.required],
    });
  }

  public onLoginClick(): void {
    if (this.loginForm.valid) {
      const login = this.loginForm.value
      this.authFacade.postLogIn$(login.username, login.password)
        .then(
          (token) => {
            this.authService.setAuthToken(token.token)
            if (this.authService.getIsAuthenticated) {
              this.router.navigate(['/dashboard-vole-papillon-damour']);
            } else {
              this._snackBar.open('Erreur lors de la connexion', 'Fermer', {
                horizontalPosition: 'end',
                verticalPosition: 'top',
              });
            }
          }
        )
        .catch(error => {
          let errorResponse = '';
          if (error.response && error.response.status === 503) {
            errorResponse = ErrorsEnum.RATE_LIMIT;
          } else if (error.response) {
            const responseData = error.response.data;

            if (responseData.errors && responseData.errors["Auth.InvalidUsername"]) {
              errorResponse = ErrorsEnum.USERNAME
            } else if (responseData.errors && responseData.errors["Auth.InvalidPassword"]) {
              errorResponse = ErrorsEnum.PASSWORD
            } else {
              errorResponse = ErrorsEnum.UNKNOWN
            }
          } else {
            console.error('Erreur de requête :', error.message);
          }
          this._snackBar.open(errorResponse, 'Fermer', {
            horizontalPosition: 'end',
            verticalPosition: 'top',
          });
        });
    } else {
      console.error("Form is invalid");
    }
  }
}
