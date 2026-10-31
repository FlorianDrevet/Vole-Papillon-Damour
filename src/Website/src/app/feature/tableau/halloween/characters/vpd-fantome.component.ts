import {Component, Input} from '@angular/core';

export type FantomeMood = 'flotte' | 'coucou' | 'lanterne' | 'course';

@Component({
  selector: 'vpd-fantome',
  standalone: true,
  templateUrl: './vpd-fantome.component.html',
  styleUrl: './vpd-fantome.component.scss',
})
export class VpdFantomeComponent {
  private static nextInstance = 0;
  @Input() size = 180;
  @Input() mood: FantomeMood = 'coucou';
  readonly instanceId = VpdFantomeComponent.nextInstance++;
}
