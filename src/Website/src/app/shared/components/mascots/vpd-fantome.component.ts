import {Component, Input, ChangeDetectionStrategy} from '@angular/core';

export type FantomeMood = 'flotte' | 'coucou' | 'lanterne' | 'course';

@Component({
  selector: 'vpd-fantome',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.Eager,
  templateUrl: './vpd-fantome.component.html',
})
export class VpdFantomeComponent {
  private static nextInstance = 0;
  @Input() size = 180;
  @Input() mood: FantomeMood = 'coucou';
  readonly instanceId = VpdFantomeComponent.nextInstance++;
}
