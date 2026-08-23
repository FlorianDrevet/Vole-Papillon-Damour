import { Component, input } from '@angular/core';

/**
 * Filet dégradé de marque + label en petites capitales, précédant un titre de
 * section. Le titre lui-même est projeté par l'appelant (niveau de heading et
 * mise en forme variables selon les pages).
 *
 * Deux orientations, toutes deux présentes dans la maquette : `top` pose un filet
 * horizontal au-dessus du label, `left` une barre verticale à gauche du bloc
 * label + titre (agenda de la page d'accueil). `lineWidthClass` ne concerne que
 * l'orientation `top`, la barre verticale s'étirant sur la hauteur du bloc.
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
  orientation = input<'top' | 'left'>('top');
}
