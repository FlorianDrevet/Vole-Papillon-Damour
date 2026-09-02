import { Component, input } from '@angular/core';

/**
 * Filet dégradé de marque + label en petites capitales, précédant un titre de
 * section. Transposé à l'identique du Website (voir
 * src/Website/src/app/shared/components/section-eyebrow) : c'est la même
 * signature visuelle qui ouvre chaque section, ici comme là-bas.
 */
@Component({
  selector: 'app-section-eyebrow',
  templateUrl: './section-eyebrow.component.html',
  standalone: false,
})
export class SectionEyebrowComponent {
  label = input.required<string>();
  labelClass = input<string>('text-blue');
}
