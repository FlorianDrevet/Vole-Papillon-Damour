import { Component, computed, inject, input } from '@angular/core';

import { HistoryReaderService } from '../../history-reader.service';

/**
 * Un chapitre du récit. Le texte de la maman est projeté tel quel par le
 * composant `year-20XX` correspondant ; ce conteneur n'apporte que l'entête
 * (rang, année, titre) et la mise en page de lecture.
 *
 * Les chapitres non ouverts restent rendus mais masqués (`hidden`) : le texte
 * intégral demeure ainsi dans le HTML servi par le rendu serveur.
 */
@Component({
  selector: 'app-history-container',
  templateUrl: './history-container.component.html',
  styleUrl: './history-container.component.scss',
  standalone: false,
})
export class HistoryContainerComponent {
  Year = input.required<number>();
  Title = input.required<string>();

  protected readonly reader = inject(HistoryReaderService);
  protected readonly position = computed(() => this.reader.positionOf(this.Year()));
  protected readonly isOpen = computed(() => this.reader.isActive(this.Year()));
}
