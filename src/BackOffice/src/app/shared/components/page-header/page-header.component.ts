import { Component, input } from '@angular/core';

/**
 * En-tête de page de liste (Actualités, Évènements) : chapeau + titre serif +
 * emplacement pour une action projetée (le bouton "Nouvelle actualité" /
 * "Créer un évènement"). Remplace le motif `text-8xl` + icône flottante de
 * l'ancien habillage par la même hiérarchie que les en-têtes de section du
 * Website (app-section-eyebrow + titre `font-serif`).
 */
@Component({
  selector: 'app-page-header',
  templateUrl: './page-header.component.html',
  standalone: false,
})
export class PageHeaderComponent {
  eyebrow = input.required<string>();
  title = input.required<string>();
}
