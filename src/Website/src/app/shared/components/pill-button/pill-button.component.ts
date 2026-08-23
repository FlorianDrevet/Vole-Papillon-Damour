import { Component, computed, input } from '@angular/core';

export type PillButtonVariant = 'primary' | 'secondary' | 'secondary-dark' | 'ghost-dark';

/**
 * Bouton "pilule" du design system. Rendu en lien (routerLink ou href) si une
 * cible est fournie, sinon en bouton de soumission (formulaires).
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
  variant = input<PillButtonVariant>('primary');
  small = input<boolean>(false);

  private readonly variantClasses: Record<PillButtonVariant, string> = {
    primary: 'bg-orange text-white hover:brightness-[1.04]',
    secondary: 'border border-mist text-blue hover:bg-blue/5',
    'secondary-dark': 'border border-cyan/40 text-mist-2 hover:bg-cyan/10',
    'ghost-dark': 'bg-cyan/10 border border-cyan/25 text-mist-2 hover:bg-cyan/[.16]',
  };

  readonly classes = computed(() => [
    'inline-flex items-center justify-center gap-2 rounded-full font-semibold transition',
    this.variantClasses[this.variant()],
    this.small() ? 'px-5 py-2.5 text-sm' : 'px-7 py-[15px] text-[15px]',
  ].join(' '));
}
