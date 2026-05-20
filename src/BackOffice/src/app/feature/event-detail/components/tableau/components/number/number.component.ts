import {Component, EventEmitter, inject, Input, Output} from '@angular/core';
import {VpdEventModel} from "../../../../../../shared/models/vpdEvent.model";
import {LotoFacadeService} from "../../../../../../shared/facades/loto.facade.service";

@Component({
    selector: 'app-number',
    templateUrl: './number.component.html',
    styleUrl: './number.component.scss',
    standalone: false
})
export class NumberComponent {
  @Input() public value: number = 0;
  @Input() public assoEventId: string = '';
  @Input() public partieId: string = '';
  @Input() public isLastValue: boolean = false;
  @Input() public isNeighbour: boolean = false;
  @Input() public isExited: boolean = false;
  @Output() public newAssoEvent = new EventEmitter<VpdEventModel>();

  lotoFacade = inject(LotoFacadeService);

  public onClick() {
    this.lotoFacade.postNumberToPartie$(this.assoEventId, this.value).then(response => {
      this.newAssoEvent.emit(response);
    })
  }

  getClass(): any {
    const classes = {
      'top-left': this.value === 1,
      'top-right': this.value === 10,
      'bottom-left': this.value === 81,
      'bottom-right': this.value === 90,
    };

    if (this.isLastValue) {
      return {
        ...classes,
        'last-value': true
      }
    }
    if (this.isNeighbour) {
      return {
        ...classes,
        'neighbour': true
      }
    }
    if (this.isExited) {
      return {
        ...classes,
        'exited': true
      }
    }
    return classes;
  }
}
