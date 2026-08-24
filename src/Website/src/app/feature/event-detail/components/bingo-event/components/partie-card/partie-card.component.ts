import {Component, computed, input} from '@angular/core';
import {VpdEventPartieModel} from "../../../../../../shared/models/vpdEvent.model";
import {compareNumberLines} from "../../../../../../shared/enums/numberLine.enum";
import {PartieTypeEnum} from "../../../../../../shared/enums/partieType.enum";

@Component({
    selector: 'app-partie-card',
    templateUrl: './partie-card.component.html',
    styleUrl: './partie-card.component.scss',
    standalone: false
})
export class PartieCardComponent {
  Partie = input.required<VpdEventPartieModel>()

  lineParties = computed(() => {
    const l = this.Partie().lineParties;
    l.sort((a, b) => compareNumberLines(a.numberLine, b.numberLine));
    return l.filter(linePartie => linePartie.lots.length > 0);
  });

  /** Les parties hors "standard" (américaine, carton plein, bingo…) sont mises en avant. */
  isSpecial = computed(() => this.Partie().partieType !== PartieTypeEnum.STANDARD);
}
