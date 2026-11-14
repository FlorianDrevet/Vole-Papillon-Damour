import { Component, HostBinding, input, ChangeDetectionStrategy } from '@angular/core';

/**
 * Emplacement de substitution pour une photo qui n'existe pas encore
 * (hachures + légende centrée). Le composant occupe tout l'espace de son
 * hôte : dimensionner via une classe (`class="h-56 rounded-md"`, etc.).
 */
@Component({
  selector: 'app-photo-placeholder',
  templateUrl: './photo-placeholder.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  standalone: false,
})
export class PhotoPlaceholderComponent {
  label = input.required<string>();
  tone = input<'light' | 'dark'>('light');

  @HostBinding('class') hostClass = 'block overflow-hidden';
}
