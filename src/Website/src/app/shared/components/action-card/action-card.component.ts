import { Component, HostBinding, computed, input } from '@angular/core';

/**
 * Carte de la mosaïque "preuves" / "nos actions" (bento grid 12 colonnes).
 * Le composant hôte reçoit sa taille via une classe Tailwind `[grid-column:span_N]`
 * posée par l'appelant ; la carte elle-même gère uniquement son contenu.
 */
@Component({
  selector: 'app-action-card',
  templateUrl: './action-card.component.html',
  standalone: false,
})
export class ActionCardComponent {
  meta = input.required<string>();
  title = input.required<string>();
  description = input<string>();
  photoLabel = input<string>();
  tone = input<'light' | 'dark'>('light');
  rotateDeg = input<number>(-1.4);
  floatDelayS = input<number>(0);

  /**
   * Le décalage est appliqué en délai *négatif* : l'animation démarre déjà en cours,
   * donc la carte est inclinée et flotte dès la première frame. Avec un délai positif
   * elle resterait droite et immobile pendant `floatDelayS` secondes avant de basculer
   * d'un coup sur son inclinaison.
   */
  protected readonly floatOffsetS = computed(() => -Math.abs(this.floatDelayS()));

  @HostBinding('class') hostClass = 'block';
}
