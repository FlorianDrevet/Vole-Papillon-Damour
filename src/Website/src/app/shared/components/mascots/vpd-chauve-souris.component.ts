import {Component, Input, ChangeDetectionStrategy} from '@angular/core';

export type ChauveSourisMood = 'vol' | 'dort';

@Component({
  selector: 'vpd-chauve-souris',
  standalone: true,
  templateUrl: './vpd-chauve-souris.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrl: './vpd-chauve-souris.component.scss',
})
export class VpdChauveSourisComponent {
  @Input() size = 200;
  @Input() mood: ChauveSourisMood = 'vol';
}
