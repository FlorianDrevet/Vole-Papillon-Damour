import {Component} from '@angular/core';

import {ScanAuthService} from './scan-auth.service';

@Component({
  selector: 'app-scan-login',
  templateUrl: './scan-login.component.html',
  styleUrl: './scan-login.component.scss',
  standalone: false,
})
export class ScanLoginComponent {
  constructor(private readonly scanAuth: ScanAuthService) {}

  get authState$() {
    return this.scanAuth.authState$;
  }

  login(): void {
    this.scanAuth.login();
  }

  logout(): void {
    this.scanAuth.logout();
  }
}
