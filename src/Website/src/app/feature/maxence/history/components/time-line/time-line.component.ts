import { Component, ElementRef, Injector, afterNextRender, inject, output } from '@angular/core';

import { HistoryReaderService } from '../../history-reader.service';

/**
 * Frise du récit : sommaire cliquable des chapitres, jauge d'avancement et
 * passerelles vers les deux autres onglets de la rubrique Maxence.
 *
 * Deux rendus pour un même sommaire : une frise verticale en colonne latérale à
 * partir de `lg`, une bande d'années défilante en dessous (la liste complète des
 * titres y prendrait tout l'écran avant le premier paragraphe).
 */
@Component({
  selector: 'app-time-line',
  templateUrl: './time-line.component.html',
  styleUrl: './time-line.component.scss',
  standalone: false,
})
export class TimeLineComponent {
  /** Rang du chapitre demandé par le lecteur. */
  readonly select = output<number>();

  protected readonly reader = inject(HistoryReaderService);

  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly injector = inject(Injector);

  /**
   * Position du curseur de la frise verticale, exprimée en hauteurs de ligne :
   * les lignes ont toutes la même hauteur (`--vpd-rail-row`), donc le curseur se
   * déplace par simple translation, animée en CSS.
   */
  protected markerTransform(): string {
    return `translateY(calc(${this.reader.activeIndex()} * var(--vpd-rail-row)))`;
  }

  /** Flèches, Origine et Fin déplacent la sélection dans la frise. */
  protected onKeydown(event: KeyboardEvent): void {
    const current = this.reader.activeIndex();
    const target = {
      ArrowDown: current + 1,
      ArrowRight: current + 1,
      ArrowUp: current - 1,
      ArrowLeft: current - 1,
      Home: 0,
      End: this.reader.total - 1,
    }[event.key];

    if (target === undefined || target < 0 || target >= this.reader.total) {
      return;
    }

    event.preventDefault();
    this.select.emit(target);

    // Tabulation glissante : le focus suit la sélection, sinon il resterait sur
    // un onglet devenu `tabindex="-1"`. `preventScroll` laisse au lecteur le
    // soin de cadrer la page sur le chapitre plutôt que sur la frise.
    afterNextRender(
      () => this.host.nativeElement
        .querySelector<HTMLButtonElement>('[role="tab"][aria-selected="true"]')
        ?.focus({ preventScroll: true }),
      { injector: this.injector },
    );
  }
}
