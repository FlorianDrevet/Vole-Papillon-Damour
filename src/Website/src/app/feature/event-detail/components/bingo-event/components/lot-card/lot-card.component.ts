import {Component, computed, input, signal} from '@angular/core';
import {VpdEventPartieLineModel, VpdEventPartieLotModel} from "../../../../../../shared/models/vpdEvent.model";
import {before} from "lodash";
import {NumberLineEnum} from "../../../../../../shared/enums/numberLine.enum";

@Component({
    selector: 'app-lot-card',
    templateUrl: './lot-card.component.html',
    styleUrl: './lot-card.component.scss',
    standalone: false
})
export class LotCardComponent {
  LinePartie = input.required<VpdEventPartieLineModel>()

  private _currentLotIndex = signal(0);
  currentLot = computed(() => this.LinePartie().lots[this._currentLotIndex()])

  nextLot() {
    this._currentLotIndex.update((index) => {
      if (index === this.LinePartie().lots.length - 1)
        return 0;
      return index + 1
    })
  }
  previousLot() {
    this._currentLotIndex.update((index) => {
      if (index === 0)
        return this.LinePartie().lots.length - 1;
      return index - 1
    });
  }

  protected readonly before = before;
}
