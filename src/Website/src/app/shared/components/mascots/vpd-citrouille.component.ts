import {Component, Input, ChangeDetectionStrategy} from '@angular/core';

export type CitrouilleMood = 'idle' | 'sourire' | 'rire' | 'dort' | 'salut';

@Component({
  selector: 'vpd-citrouille',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.Eager,
  templateUrl: './vpd-citrouille.component.html',
})
export class VpdCitrouilleComponent {
  private static nextInstance = 0;
  @Input() size = 220;
  @Input() mood: CitrouilleMood = 'idle';
  readonly instanceId = VpdCitrouilleComponent.nextInstance++;
}
