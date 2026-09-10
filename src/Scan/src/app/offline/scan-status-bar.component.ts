import {Component, EventEmitter, Output} from '@angular/core';

import {ScanStatusService} from './scan-status.service';

@Component({
  selector: 'scan-status-bar',
  templateUrl: './scan-status-bar.component.html',
  styleUrls: ['./scan-status-bar.component.scss'],
  standalone: false,
})
export class ScanStatusBarComponent {
  @Output() readonly syncRequested = new EventEmitter<void>();
  @Output() readonly recoveryRequested = new EventEmitter<void>();

  constructor(readonly status: ScanStatusService) {}
}
