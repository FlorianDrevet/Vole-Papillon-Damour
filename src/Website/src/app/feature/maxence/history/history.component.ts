import { Component, ElementRef, Injector, afterNextRender, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Location } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';

import { HistoryReaderService } from './history-reader.service';

/** Préfixe des ancres de chapitre, historique : `#date-2004`, `#date-2005`… */
const CHAPTER_FRAGMENT_PREFIX = 'date-';

interface SectionLink {
  label: string;
  route: string;
}

/** Raccourcis vers l'onglet « Ses maladies », dans l'ordre des fiches. */
const DISEASE_LINKS: SectionLink[] = [
  { label: 'Hirschsprung', route: '/maxence/maladies/hirschsprung' },
  { label: 'P.O.I.C.', route: '/maxence/maladies/poic' },
  { label: 'Dysplasie ectodermique', route: '/maxence/maladies/dysplasie-ectodermique' },
  { label: 'Dystrophie FSH', route: '/maxence/maladies/fshd' },
  { label: 'Neuropathie', route: '/maxence/maladies/neuropathie' },
  { label: 'Ostéoporose', route: '/maxence/maladies/osteoporose' },
  { label: 'Hyperthyroïdie', route: '/maxence/maladies/hyperthyroidie' },
  { label: 'Wolff-Parkinson-White', route: '/maxence/maladies/wolff-parkinson-white' },
];

/** Raccourcis vers l'onglet « Son quotidien, ses combats ». */
const DAILY_LIFE_LINKS: SectionLink[] = [
  { label: 'Soins quotidiens', route: '/maxence/vie-quotidienne/soins-quotidiens' },
  { label: 'Soins hospitaliers', route: '/maxence/vie-quotidienne/soins-hospitaliers' },
  { label: 'École', route: '/maxence/vie-quotidienne/ecole' },
  { label: 'Greffe', route: '/maxence/vie-quotidienne/greffe' },
  { label: 'Nutrition', route: '/maxence/maladies/gastrostomie' },
];

/**
 * Page « L'histoire de Maxence ». Le récit est long : plutôt que de le dérouler
 * d'un seul tenant, il est lu chapitre par chapitre (frise à gauche, boutons
 * précédent / suivant en pied de lecture).
 *
 * Les treize chapitres restent tous dans le DOM, seuls masqués : le rendu serveur
 * expose donc l'intégralité du texte aux moteurs de recherche, et les ancres
 * `#date-<année>` déjà partagées continuent d'ouvrir le bon chapitre.
 */
@Component({
  selector: 'app-history',
  templateUrl: './history.component.html',
  styleUrl: './history.component.scss',
  standalone: false,
  providers: [HistoryReaderService],
})
export class HistoryComponent {
  protected readonly reader = inject(HistoryReaderService);
  protected readonly diseaseLinks = DISEASE_LINKS;
  protected readonly dailyLifeLinks = DAILY_LIFE_LINKS;

  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly location = inject(Location);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly injector = inject(Injector);

  constructor() {
    // Ouvre le chapitre visé par l'ancre à l'arrivée sur la page, et à chaque
    // fois qu'un lien externe à la page change l'ancre.
    this.route.fragment.pipe(takeUntilDestroyed()).subscribe(fragment => {
      const year = Number(fragment?.replace(CHAPTER_FRAGMENT_PREFIX, ''));
      if (Number.isInteger(year)) {
        this.reader.openYear(year);
      }
    });
  }

  /** Ouvre un chapitre depuis la frise ou le pied de lecture. */
  protected open(index: number): void {
    if (!this.reader.open(index)) {
      return;
    }

    // `replaceState` plutôt qu'une navigation : l'ancre reste partageable sans
    // empiler une entrée d'historique par chapitre, et sans déclencher le
    // défilement automatique du routeur (`anchorScrolling`), qui doublonnerait
    // celui déclenché ci-dessous une fois le nouveau chapitre rendu.
    const fragment = CHAPTER_FRAGMENT_PREFIX + this.reader.activeChapter().year;
    this.location.replaceState(
      this.router.createUrlTree([], { relativeTo: this.route, fragment }).toString(),
    );

    afterNextRender(() => this.scrollToActiveChapter(), { injector: this.injector });
  }

  private scrollToActiveChapter(): void {
    const panel = this.host.nativeElement.querySelector(
      `#${CHAPTER_FRAGMENT_PREFIX}${this.reader.activeChapter().year}`,
    );
    panel?.scrollIntoView({ block: 'start' });
  }
}
