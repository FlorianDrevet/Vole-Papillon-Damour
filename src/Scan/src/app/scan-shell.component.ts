import {Component, Signal} from '@angular/core';
import {toSignal} from '@angular/core/rxjs-interop';

import {ScanAuthService} from './auth/scan-auth.service';

@Component({
  selector: 'app-scan-shell',
  templateUrl: './scan-shell.component.html',
  styleUrl: './scan-shell.component.scss',
  standalone: false,
})
export class ScanShellComponent {
  private readonly authStateSignal: Signal<ScanAuthService['authState']>;

  constructor(readonly scanAuth: ScanAuthService) {
    this.authStateSignal = toSignal(this.scanAuth.authState$, {
      initialValue: this.scanAuth.authState,
    });
  }

  get isBootstrapping(): boolean {
    return this.authStateSignal().status === 'checking';
  }

  get isAuthenticated(): boolean {
    const state = this.authStateSignal().status;
    return state === 'authorized' || state === 'degraded';
  }
}
