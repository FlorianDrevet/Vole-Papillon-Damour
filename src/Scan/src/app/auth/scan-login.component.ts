import {Component, signal} from '@angular/core';

import {ScanAuthService} from './scan-auth.service';

@Component({
  selector: 'app-scan-login',
  templateUrl: './scan-login.component.html',
  styleUrl: './scan-login.component.scss',
  standalone: false,
})
export class ScanLoginComponent {
  readonly loginError = signal<string | null>(null);
  readonly loginInProgress = signal(false);

  constructor(private readonly scanAuth: ScanAuthService) {}

  get authState$() {
    return this.scanAuth.authState$;
  }

  login(): void {
    this.loginError.set(null);
    this.loginInProgress.set(true);

    try {
      this.scanAuth.login().subscribe({
        error: () => this.showLoginError(),
        complete: () => this.loginInProgress.set(false),
      });
    } catch {
      this.showLoginError();
    }
  }

  logout(): void {
    this.scanAuth.logout();
  }

  private showLoginError(): void {
    this.loginInProgress.set(false);
    this.loginError.set('Impossible de démarrer la connexion. Réessayez.');
  }
}
