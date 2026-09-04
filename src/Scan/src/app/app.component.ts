import {Component} from '@angular/core';
import {ScanAuthService} from './auth/scan-auth.service';

@Component({
  selector: 'app-root',
  template: `
    <ng-container *ngIf="scanAuth.authState$ | async as authState">
      <app-scanner *ngIf="authState.status === 'authorized'; else login"></app-scanner>
      <ng-template #login><app-scan-login></app-scan-login></ng-template>
    </ng-container>
  `,
  standalone: false,
})
export class AppComponent {
  constructor(readonly scanAuth: ScanAuthService) {}
}
