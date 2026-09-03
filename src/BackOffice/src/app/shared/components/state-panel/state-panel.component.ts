import {Component, computed, input} from '@angular/core';

import {IconName} from '../icon/icon.component';

export type StatePanelTone = 'neutral' | 'danger';

/**
 * Bloc d'état d'une liste : « rien à afficher » ou « le chargement a échoué ».
 *
 * Les pages se contentaient d'une phrase grise, et une requête en échec laissait
 * un indicateur de chargement tourner indéfiniment sans jamais dire ce qui se
 * passait ni comment réessayer. Les actions sont projetées par la page appelante.
 */
@Component({
  selector: 'app-state-panel',
  templateUrl: './state-panel.component.html',
  standalone: false,
})
export class StatePanelComponent {
  icon = input.required<IconName>();
  title = input.required<string>();
  description = input<string>('');
  tone = input<StatePanelTone>('neutral');

  protected readonly containerClasses = computed(() =>
    this.tone() === 'danger'
      ? 'border-danger/30 bg-danger-soft'
      : 'border-line bg-white',
  );

  protected readonly iconClasses = computed(() =>
    this.tone() === 'danger'
      ? 'border-danger/30 bg-white text-danger'
      : 'border-line-2 bg-paper-2 text-blue',
  );
}
