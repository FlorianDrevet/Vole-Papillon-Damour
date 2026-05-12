import {AfterViewInit, Component, EventEmitter, Input, Output} from '@angular/core';

@Component({
  selector: 'app-modal',
  templateUrl: './modal.component.html',
  styleUrl: './modal.component.scss'
})
export class ModalComponent implements AfterViewInit {
  @Input() public showModal = false;
  @Output() public closeModal = new EventEmitter<boolean>();

  closeModalClicked() {
    this.closeModal.emit(false);
    document.body.classList.remove('modal-open');
  }

  ngAfterViewInit(): void {
    document.body.classList.add('modal-open');
  }
}
