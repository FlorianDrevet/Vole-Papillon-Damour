import { Component, computed, input } from '@angular/core';
import { IconName } from '../icon/icon.component';

export type IconButtonVariant = 'default' | 'danger';

/**
 * Bouton rond icône-seule du design system : remplace le motif répété
 * `rounded-full bg-white border-primary-color border-2 p-2` + `<img>` PNG
 * (crayon/supprimer/plus…) utilisé partout dans l'ancien BackOffice.
 *
 * `ariaLabel` est requis : ces boutons n'ont pas de texte visible, un
 * lecteur d'écran n'a que ça pour les identifier.
 */
@Component({
  selector: 'app-icon-button',
  templateUrl: './icon-button.component.html',
  standalone: false,
})
export class IconButtonComponent {
  icon = input.required<IconName>();
  ariaLabel = input.required<string>();
  variant = input<IconButtonVariant>('default');
  small = input<boolean>(false);
  disabled = input<boolean>(false);
  type = input<'button' | 'submit'>('button');

  private readonly variantClasses: Record<IconButtonVariant, string> = {
    default: 'border-line bg-white text-slate-2 hover:border-blue-2 hover:text-blue',
    danger: 'border-danger/30 bg-white text-danger hover:bg-danger-soft',
  };

  readonly classes = computed(() => [
    'inline-flex shrink-0 items-center justify-center rounded-full border transition disabled:pointer-events-none disabled:opacity-40',
    this.variantClasses[this.variant()],
    this.small() ? 'h-8 w-8' : 'h-10 w-10',
  ].join(' '));

  readonly iconSize = computed(() => (this.small() ? 15 : 17));
}
