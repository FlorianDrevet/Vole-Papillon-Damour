import {ChangeDetectionStrategy, Component, EventEmitter, Input, Output} from '@angular/core';

@Component({
  selector: 'app-catalog-auth-prompt',
  standalone: false,
  templateUrl: './catalog-auth-prompt.component.html',
  styleUrls: ['./catalog-auth-prompt.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CatalogAuthPromptComponent {
  @Input() open = false;
  @Output() readonly closed = new EventEmitter<void>();
  @Output() readonly loginRequested = new EventEmitter<void>();
  @Output() readonly registerRequested = new EventEmitter<void>();

  requestClose(): void {
    this.closed.emit();
  }

  requestLogin(): void {
    this.loginRequested.emit();
  }

  requestRegister(): void {
    this.registerRequested.emit();
  }
}
