import {Component, ChangeDetectionStrategy} from '@angular/core';

import {ScanConfirmationService} from './scan-confirmation.service';

@Component({
  selector: 'app-scan-confirmation',
  templateUrl: './scan-confirmation.component.html',
  styleUrl: './scan-confirmation.component.scss',
  changeDetection: ChangeDetectionStrategy.Eager,
  standalone: false,
})
export class ScanConfirmationComponent {
  constructor(readonly confirmation: ScanConfirmationService) {}

  onCoverError(event: Event): void {
    const image = event.currentTarget as HTMLImageElement | null;
    if (image) {
      image.hidden = true;
    }
  }
}
