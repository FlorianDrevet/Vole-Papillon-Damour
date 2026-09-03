import {Component, computed, input} from '@angular/core';

export type FilterChipVariant = 'tab' | 'chip';

/**
 * Bouton de filtre de la caisse.
 *
 * Les sept filtres étaient écrits à la main, chacun avec sa propre chaîne de
 * classes recopiée : les styles avaient déjà divergé entre les deux rangées, et
 * aucun ne signalait son état sélectionné à un lecteur d'écran.
 */
@Component({
  selector: 'app-filter-chip',
  templateUrl: './filter-chip.component.html',
  standalone: false,
})
export class FilterChipComponent {
  active = input.required<boolean>();
  variant = input<FilterChipVariant>('tab');

  protected readonly classes = computed(() => {
    const base = 'inline-flex items-center gap-2 rounded-full border transition';

    if (this.variant() === 'chip') {
      return [
        base,
        'px-4 py-2 font-mono text-[11px] uppercase tracking-[.1em]',
        this.active()
          ? 'border-blue bg-blue text-white'
          : 'border-transparent bg-paper-2 text-slate-2 hover:bg-mist-2',
      ].join(' ');
    }

    return [
      base,
      'px-5 py-2.5 text-sm font-semibold',
      this.active()
        ? 'border-orange bg-orange text-white'
        : 'border-line bg-white text-slate-2 hover:border-mist',
    ].join(' ');
  });
}
