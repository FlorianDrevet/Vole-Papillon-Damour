import {ChangeDetectionStrategy, Component, EventEmitter, Input, Output} from '@angular/core';

@Component({
  selector: 'app-browser-storage-banner',
  standalone: false,
  templateUrl: './browser-storage-banner.component.html',
  styleUrls: ['./browser-storage-banner.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BrowserStorageBannerComponent {
  @Input() collapsed = false;
  @Input() available = true;
  @Output() readonly collapsedChange = new EventEmitter<boolean>();
  @Output() readonly createAccountRequested = new EventEmitter<void>();

  toggle(): void {
    this.collapsedChange.emit(!this.collapsed);
  }
}
