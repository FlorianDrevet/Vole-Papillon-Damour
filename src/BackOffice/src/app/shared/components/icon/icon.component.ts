import { Component, input } from '@angular/core';

export type IconName =
  | 'add'
  | 'back'
  | 'calendar'
  | 'check'
  | 'chevron-right'
  | 'close'
  | 'delete'
  | 'download'
  | 'edit'
  | 'menu'
  | 'trophy'
  | 'undo';

/**
 * Jeu d'icônes du BackOffice, en SVG inline.
 *
 * L'ancien habillage utilisait des PNG hétérogènes (crayon.png, supprimer.png,
 * trophee.png…) : traits, épaisseurs et couleurs variaient d'une icône à
 * l'autre et aucune ne pouvait suivre la couleur du texte. Ici tout est tracé au
 * même gabarit — grille 24, `currentColor`, trait 1.6 — comme les rares SVG
 * inline du Website (chevron du sous-menu de navigation).
 *
 * Les concepts déjà couverts par un SVG du design system (calendrier, horloge,
 * lieu, porte…) continuent d'utiliser ces fichiers directement via `<img>`
 * (voir section-infos-event) plutôt que d'être redessinés ici en double.
 *
 * Les PNG restent dans public/icons/ (aucun asset supprimé) mais ne sont plus
 * référencés par l'application.
 */
@Component({
  selector: 'app-icon',
  templateUrl: './icon.component.html',
  standalone: false,
})
export class IconComponent {
  name = input.required<IconName>();
  /** Côté du carré de dessin, en pixels. */
  size = input<number>(18);
}
