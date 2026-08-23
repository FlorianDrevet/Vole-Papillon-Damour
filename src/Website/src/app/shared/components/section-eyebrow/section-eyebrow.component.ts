import { Component, input } from '@angular/core';

/**
 * Filet dégradé de marque + label en petites capitales, précédant un titre de
 * section. Le titre lui-même est projeté par l'appelant (niveau de heading et
 * mise en forme variables selon les pages).
 */
@Component({
  selector: 'app-section-eyebrow',
  templateUrl: './section-eyebrow.component.html',
  standalone: false,
})
export class SectionEyebrowComponent {
  label = input.required<string>();
  labelClass = input<string>('text-blue');
  lineWidthClass = input<string>('w-16');
}
