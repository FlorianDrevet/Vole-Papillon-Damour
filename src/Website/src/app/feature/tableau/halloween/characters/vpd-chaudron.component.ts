import {Component, Input} from '@angular/core';

@Component({
  selector: 'vpd-chaudron',
  standalone: true,
  templateUrl: './vpd-chaudron.component.html',
  styleUrl: './vpd-chaudron.component.scss',
})
export class VpdChaudronComponent {
  private static nextInstance = 0;
  @Input() size = 200;
  @Input() mood = 'bouillonne';
  readonly instanceId = VpdChaudronComponent.nextInstance++;
}
