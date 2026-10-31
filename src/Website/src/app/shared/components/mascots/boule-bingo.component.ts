import {Component, Input} from '@angular/core';

export type BouleBingoColor = 'bleu' | 'orange' | 'cyan';
export type BouleBingoMood = 'saute' | 'pose';

@Component({
  selector: 'vpd-boule-bingo',
  standalone: true,
  templateUrl: './boule-bingo.component.html',
})
export class VpdBouleBingoComponent {
  private static nextInstance = 0;
  @Input() size = 160;
  @Input() num = '7';
  @Input() color: BouleBingoColor = 'bleu';
  @Input() mood: BouleBingoMood = 'saute';
  readonly instanceId = VpdBouleBingoComponent.nextInstance++;
  readonly palettes: Record<BouleBingoColor, readonly [string, string, string]> = {
    bleu: ['#6cc4ef', '#1497d6', '#0a5a8a'],
    orange: ['#ffc07a', '#f0801c', '#b8560b'],
    cyan: ['#d4f3fd', '#7fd8f5', '#3aa8cf'],
  };
}
