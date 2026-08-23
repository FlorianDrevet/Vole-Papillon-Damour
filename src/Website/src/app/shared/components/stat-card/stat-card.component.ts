import { Component, input } from '@angular/core';

/**
 * Carte "chiffre clé" flottante et légèrement inclinée (animation vpdFloat).
 * Fond clair ou encre selon `tone`.
 */
@Component({
  selector: 'app-stat-card',
  templateUrl: './stat-card.component.html',
  standalone: false,
})
export class StatCardComponent {
  value = input.required<string>();
  label = input.required<string>();
  description = input<string>();
  tone = input<'light' | 'dark'>('light');
  rotateDeg = input<number>(-1.2);
  floatDelayS = input<number>(0);
  floatDurationS = input<number>(8);
}
