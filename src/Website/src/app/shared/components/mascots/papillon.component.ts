import {Component, Input, ChangeDetectionStrategy} from '@angular/core';

export type PapillonMood = 'vol' | 'pose';

@Component({
  selector: 'vpd-papillon',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.Eager,
  templateUrl: './papillon.component.html',
})
export class VpdPapillonComponent {
  private static nextInstance = 0;
  @Input() size = 220;
  @Input() mood: PapillonMood = 'vol';
  readonly instanceId = VpdPapillonComponent.nextInstance++;
}
