import { Component, computed, input } from '@angular/core';

export type PillButtonVariant = 'primary' | 'secondary' | 'danger';

/**
 * Bouton "pilule" du design system, transposé du Website (voir
 * src/Website/src/app/shared/components/pill-button). Rendu en lien
 * (routerLink ou href) si une cible est fournie, sinon en bouton — de
 * soumission par défaut, `type` permet de forcer `button` pour les actions
 * qui ouvrent un dialogue plutôt que de soumettre un formulaire.
 *
 * `danger` remplace la variante secondaire du Website pour les actions
 * destructrices (confirmation de suppression) : le BackOffice en a besoin,
 * le Website non.
 */
@Component({
  selector: 'app-pill-button',
  templateUrl: './pill-button.component.html',
  standalone: false,
})
export class PillButtonComponent {
  routerLink = input<string | any[]>();
  href = input<string>();
  target = input<string>();
  type = input<'button' | 'submit'>('submit');
  variant = input<PillButtonVariant>('primary');
  small = input<boolean>(false);
  disabled = input<boolean>(false);

  private readonly variantClasses: Record<PillButtonVariant, string> = {
    primary: 'bg-orange text-white hover:brightness-[1.04]',
    secondary: 'border border-mist text-blue hover:bg-blue/5',
    danger: 'border border-danger/40 text-danger hover:bg-danger-soft',
  };

  readonly classes = computed(() => [
    'inline-flex items-center justify-center gap-2 rounded-full font-semibold transition disabled:pointer-events-none disabled:opacity-50',
    this.variantClasses[this.variant()],
    this.small() ? 'px-5 py-2.5 text-sm' : 'px-7 py-[15px] text-[15px]',
  ].join(' '));
}
