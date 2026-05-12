import {isPlatformBrowser} from '@angular/common';
import {AfterViewInit, Component, EventEmitter, inject, Input, Output, PLATFORM_ID} from '@angular/core';

@Component({
  selector: 'app-modal',
  templateUrl: './modal.component.html',
  styleUrl: './modal.component.scss',
  standalone: false
})
export class ModalComponent implements AfterViewInit {
  private readonly platformId = inject(PLATFORM_ID);

  @Input() public showModal = false;
  @Output() public closeModal = new EventEmitter<boolean>();

  closeModalClicked() {
    this.closeModal.emit(false);
    if (!isPlatformBrowser(this.platformId)) {
      return;
    }

    globalThis.document.body.classList.remove('modal-open');
  }

  ngAfterViewInit(): void {
    if (!isPlatformBrowser(this.platformId)) {
      return;
    }

    globalThis.document.body.classList.add('modal-open');
  }
}
