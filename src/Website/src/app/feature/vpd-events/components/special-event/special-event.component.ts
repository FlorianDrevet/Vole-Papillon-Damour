import { Component, computed, input } from '@angular/core';
import { VpdEventModel } from '../../../../shared/models/vpdEvent.model';

/** Teinte d'accent d'un pilier : le loto en orange, la bourse aux livres en bleu. */
export type SpecialEventAccent = 'orange' | 'blue';

/**
 * Carte "pilier" des deux grands rendez-vous de l'association (loto et bourse aux
 * livres), sur la page Évènements.
 *
 * Les photos et le texte descriptif sont *permanents* : ils décrivent le rendez-vous
 * lui-même, pas une édition en particulier, et restent donc affichés même quand aucune
 * date n'est encore programmée. Seul le bandeau "Prochaine date" dépend des données de
 * l'API et bascule sur un message d'attente si `NextEvent` est nul.
 */
@Component({
    selector: 'app-special-event',
    templateUrl: './special-event.component.html',
    standalone: false
})
export class SpecialEventComponent {
  /** Petit label de survol de la photo principale, ex. « 01 · Le loto ». */
  Kicker = input.required<string>();
  Title = input.required<string>();
  /** Photo principale en premier, puis jusqu'à deux vignettes pour la mosaïque. */
  Photos = input.required<string[]>();
  /** Points clés permanents du rendez-vous (puces sous la description). */
  Highlights = input<string[]>([]);
  Accent = input<SpecialEventAccent>('blue');
  /** Prochaine occurrence connue, ou `null` tant qu'aucune date n'est publiée. */
  NextEvent = input<VpdEventModel | null>(null);
  /** Nombre total d'occurrences à venir de ce type, prochaine date incluse. */
  UpcomingCount = input<number>(0);
  Loading = input<boolean>(false);

  readonly mainPhoto = computed(() => this.Photos()[0]);
  readonly thumbnails = computed(() => this.Photos().slice(1, 3));

  readonly detailLink = computed(() => {
    const event = this.NextEvent();
    return event ? ['/evenement', event.id] : null;
  });

  /** Dates restantes *après* celle mise en avant, pour la mention « + N autres dates ». */
  readonly otherDatesCount = computed(() => Math.max(this.UpcomingCount() - 1, 0));

  private static readonly accentClasses: Record<SpecialEventAccent, {
    text: string, chip: string, box: string, bullet: string
  }> = {
    orange: {
      text: 'text-orange-3',
      chip: 'bg-orange text-white',
      box: 'border-orange/30 bg-orange/[.07]',
      bullet: 'bg-orange',
    },
    blue: {
      text: 'text-blue',
      chip: 'bg-blue text-white',
      box: 'border-blue-2/30 bg-blue-2/[.07]',
      bullet: 'bg-blue-2',
    },
  };

  private readonly accent = computed(() => SpecialEventComponent.accentClasses[this.Accent()]);

  readonly accentTextClass = computed(() => this.accent().text);
  readonly dateChipClass = computed(() => this.accent().chip);
  readonly dateBoxClass = computed(() => this.accent().box);
  readonly bulletClass = computed(() => this.accent().bullet);
}
