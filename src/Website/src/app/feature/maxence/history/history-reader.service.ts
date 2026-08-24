import { Injectable, computed, signal } from '@angular/core';

import { HISTORY_CHAPTERS, HistoryChapter } from './history-chapters';

/**
 * État de lecture du récit : quel chapitre est ouvert.
 *
 * Fourni par `HistoryComponent` (et non `providedIn: 'root'`) : l'état ne doit
 * vivre que le temps de la page. La frise le lit pour marquer l'étape courante,
 * chaque `app-history-container` pour savoir s'il doit s'afficher — ce qui évite
 * de faire transiter l'état à travers les treize composants `year-20XX`.
 */
@Injectable()
export class HistoryReaderService {
  readonly chapters = HISTORY_CHAPTERS;
  readonly total = HISTORY_CHAPTERS.length;

  private readonly index = signal(0);

  readonly activeIndex = this.index.asReadonly();
  readonly activeChapter = computed<HistoryChapter>(() => this.chapters[this.index()]);
  readonly previous = computed<HistoryChapter | null>(() => this.chapters[this.index() - 1] ?? null);
  readonly next = computed<HistoryChapter | null>(() => this.chapters[this.index() + 1] ?? null);

  /** Avancement dans le récit, en pourcentage, pour la jauge de la frise. */
  readonly progress = computed(() => ((this.index() + 1) / this.total) * 100);

  isActive(year: number): boolean {
    return this.activeChapter().year === year;
  }

  /** Rang affiché du chapitre (1-indexé), ou 0 si l'année est inconnue. */
  positionOf(year: number): number {
    return this.chapters.findIndex(chapter => chapter.year === year) + 1;
  }

  /** Ouvre un chapitre par son rang. Retourne `false` si rien n'a changé. */
  open(index: number): boolean {
    if (index < 0 || index >= this.total || index === this.index()) {
      return false;
    }
    this.index.set(index);
    return true;
  }

  /** Ouvre un chapitre par son année (cible des ancres `#date-<year>`). */
  openYear(year: number): boolean {
    return this.open(this.chapters.findIndex(chapter => chapter.year === year));
  }
}
