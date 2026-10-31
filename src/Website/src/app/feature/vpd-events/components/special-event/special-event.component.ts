import {Component, computed, input, signal} from '@angular/core';

/** Teinte d'accent d'un rendez-vous permanent : orange pour le loto, bleu pour les livres. */
export type SpecialEventAccent = 'orange' | 'blue';

@Component({
  selector: 'app-special-event',
  templateUrl: './special-event.component.html',
  standalone: false,
})
export class SpecialEventComponent {
  private static nextId = 0;

  Kicker = input.required<string>();
  Title = input.required<string>();
  Photos = input.required<string[]>();
  Highlights = input<string[]>([]);
  Accent = input<SpecialEventAccent>('blue');
  HasMore = input(false);

  readonly isExpanded = signal(false);
  readonly detailsId = `special-event-details-${SpecialEventComponent.nextId++}`;
  readonly mainPhoto = computed(() => this.Photos()[0]);
  readonly thumbnails = computed(() => this.Photos().slice(1, 3));

  readonly bulletClass = computed(() => this.Accent() === 'orange' ? 'bg-orange' : 'bg-blue-2');

  toggleDetails(): void {
    this.isExpanded.update(expanded => !expanded);
  }
}
