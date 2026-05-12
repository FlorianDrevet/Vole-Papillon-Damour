import {Component, input} from '@angular/core';
import {BingoCardInterface} from "../../interfaces/bingoCard.interface";

@Component({
    selector: 'app-bingo-card',
    templateUrl: './bingo-card.component.html',
    styleUrl: './bingo-card.component.scss',
    standalone: false
})
export class BingoCardComponent {
  public bingoCard = input.required<BingoCardInterface>();

  public getNumberFromBingoCard(index: number): number | null {

    const start = (index % 9) * 10;
    let end = (index % 9) * 10 + 9;

    if (end > 80) {
      end++; // to include 90
    }

    if (Math.floor(index / 9) === 0) {
      return this.bingoCard().firstLine.find(number => number >= start && number <= end) ?? null;
    } else if (Math.floor(index / 9) === 1) {
      return this.bingoCard().secondLine.find(number => number >= start && number <= end) ?? null;
    } else if (Math.floor(index / 9) === 2) {
      return this.bingoCard().thirdLine.find(number => number >= start && number <= end) ?? null;
    }
    return null;
  }
}
