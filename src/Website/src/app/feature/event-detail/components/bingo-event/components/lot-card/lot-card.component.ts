import {Component, computed, input, signal} from '@angular/core';
import {VpdEventPartieLineModel} from "../../../../../../shared/models/vpdEvent.model";

@Component({
    selector: 'app-lot-card',
    templateUrl: './lot-card.component.html',
    styleUrl: './lot-card.component.scss',
    standalone: false
})
export class LotCardComponent {
  LinePartie = input.required<VpdEventPartieLineModel>()

  private _currentLotIndex = signal(0);
  currentLotIndex = this._currentLotIndex.asReadonly();
  currentLot = computed(() => this.LinePartie().lots[this._currentLotIndex()])

  /** Une ligne peut offrir plusieurs lots au choix : les flèches n'ont de sens que dans ce cas. */
  hasSeveralLots = computed(() => this.LinePartie().lots.length > 1)

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
}
