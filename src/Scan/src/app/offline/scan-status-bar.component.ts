import {Component, EventEmitter, Input, Output, ChangeDetectionStrategy} from '@angular/core';

@Component({
  selector: 'scan-status-bar',
  templateUrl: './scan-status-bar.component.html',
  styleUrls: ['./scan-status-bar.component.scss'],
  changeDetection: ChangeDetectionStrategy.Eager,
  standalone: false,
})
export class ScanStatusBarComponent {
  @Input() offline = false;
  @Input() actionRequired = false;
  @Input() pendingRarePhotos = 0;
  @Input() detail = '';
  @Output() readonly statusRequested = new EventEmitter<void>();

  get isVisible(): boolean {
    return this.offline || this.actionRequired;
  }
}
