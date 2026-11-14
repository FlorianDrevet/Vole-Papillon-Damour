import { Component, input, ChangeDetectionStrategy } from '@angular/core';
import { ActualityModel } from '../../models/actuality.model';

@Component({
  selector: 'app-actuality-card',
  templateUrl: './actuality-card.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  standalone: false,
})
export class ActualityCardComponent {
  ActualityModel = input.required<ActualityModel>();

  excerpt(text: string, max = 130): string {
    return text.length > max ? `${text.slice(0, max)}…` : text;
  }
}
