import { Component, input } from '@angular/core';
import { ActualityModel } from '../../models/actuality.model';

/**
 * Wrapper Website autour de `vpd-actuality-card`. Mode lecture seule.
 * Conserve le selector `app-actuality-card` historique.
 */
@Component({
  selector: 'app-actuality-card',
  templateUrl: './actuality-card.component.html',
  standalone: false,
})
export class ActualityCardComponent {
  ActualityModel = input.required<ActualityModel>();
}
