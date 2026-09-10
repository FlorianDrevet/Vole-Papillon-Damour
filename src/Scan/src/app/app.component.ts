import {DOCUMENT} from '@angular/common';
import {Component, Inject} from '@angular/core';
import {isScanDiagnosticRequested} from './offline/scan-diagnostic-export';

@Component({
  selector: 'app-root',
  template: `
    <app-scan-diagnostic *ngIf="diagnosticRequested; else routedSurface"></app-scan-diagnostic>
    <ng-template #routedSurface><router-outlet></router-outlet></ng-template>
  `,
  standalone: false,
})
export class AppComponent {
  diagnosticRequested: boolean;

  constructor(
    @Inject(DOCUMENT) document: Document,
  ) {
    this.diagnosticRequested = isScanDiagnosticRequested(document.location?.search ?? '');
  }
}
