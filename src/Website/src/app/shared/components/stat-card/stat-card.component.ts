import { Component, computed, input } from '@angular/core';

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
  /** Suffixe d'unité rendu plus petit à la suite du chiffre (« 100 % », « 1-3 € », « 40+ »). */
  unit = input<string>();
  label = input.required<string>();
  description = input<string>();
  tone = input<'light' | 'dark'>('light');
  rotateDeg = input<number>(-1.2);
  floatDelayS = input<number>(0);
  floatDurationS = input<number>(8);

  /**
   * Le décalage est appliqué en délai *négatif* : l'animation démarre déjà en cours,
   * donc la carte est inclinée et flotte dès la première frame. Avec un délai positif
   * elle resterait droite et immobile pendant `floatDelayS` secondes avant de basculer
   * d'un coup sur son inclinaison.
   */
  protected readonly floatOffsetS = computed(() => -Math.abs(this.floatDelayS()));
}
