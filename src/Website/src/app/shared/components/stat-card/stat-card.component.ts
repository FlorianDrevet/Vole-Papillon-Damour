import { Component, input } from '@angular/core';

/** Carte « chiffre clé » avec fond clair ou encre selon `tone`. */
@Component({
  selector: 'app-stat-card',
  templateUrl: './stat-card.component.html',
  standalone: false,
})
export class StatCardComponent {
  value = input.required<string>();
  /** Suffixe d'unité rendu plus petit à la suite du chiffre (« 100 % », « 1-3 € », « 40+ »). */
  unit = input<string>();
  label = input.required<string>();
  description = input<string>();
  tone = input<'light' | 'dark'>('light');
}
