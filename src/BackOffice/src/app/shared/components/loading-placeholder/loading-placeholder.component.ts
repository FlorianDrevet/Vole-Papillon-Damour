import {Component, computed, input} from '@angular/core';

export type LoadingPlaceholderVariant = 'cards' | 'rows' | 'tiles';

/**
 * Chargement progressif : des blocs aux dimensions du contenu réel, avec le
 * reflet discret défini dans `styles.scss` (`.skeleton`), plutôt qu'un rond qui
 * tourne au milieu d'une page vide.
 *
 * C'est le motif déjà retenu par le Website ; le BackOffice affichait encore un
 * `mat-spinner`, qui ne dit ni ce qui arrive ni combien de temps ça prend, et qui
 * fait sauter toute la mise en page au moment où les données arrivent.
 */
@Component({
  selector: 'app-loading-placeholder',
  templateUrl: './loading-placeholder.component.html',
  standalone: false,
})
export class LoadingPlaceholderComponent {
  variant = input<LoadingPlaceholderVariant>('cards');
  count = input<number>(6);

  protected readonly items = computed(() => Array.from({length: this.count()}, (_, index) => index));
}
