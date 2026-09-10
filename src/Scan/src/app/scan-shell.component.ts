import {Component} from '@angular/core';

import {ScanAuthService} from './auth/scan-auth.service';

@Component({
  selector: 'app-scan-shell',
  templateUrl: './scan-shell.component.html',
  styleUrl: './scan-shell.component.scss',
  standalone: false,
})
export class ScanShellComponent {
  constructor(readonly scanAuth: ScanAuthService) {}

  get isAuthenticated(): boolean {
    const state = this.scanAuth.authState.status;
    return state === 'authorized' || state === 'degraded';
  }
}
