import {Component} from '@angular/core';

import {ScanConfirmationService} from './scan-confirmation.service';

@Component({
  selector: 'app-scan-confirmation',
  templateUrl: './scan-confirmation.component.html',
  styleUrl: './scan-confirmation.component.scss',
  standalone: false,
})
export class ScanConfirmationComponent {
  constructor(readonly confirmation: ScanConfirmationService) {}
}
