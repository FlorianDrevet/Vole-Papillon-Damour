import {ChangeDetectionStrategy, Component, EventEmitter, Input, Output} from '@angular/core';

import {CatalogFavoriteSuggestion} from '../../core/catalog.models';

@Component({
  selector: 'app-suggestion-grid',
  standalone: false,
  templateUrl: './suggestion-grid.component.html',
  styleUrls: ['./suggestion-grid.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SuggestionGridComponent {
  @Input() suggestions: CatalogFavoriteSuggestion[] = [];
  @Input() minimumHearts = 3;
  @Output() readonly minimumHeartsChange = new EventEmitter<number>();

  recommendationText(suggestion: CatalogFavoriteSuggestion): string {
    return suggestion.reason === 'NextTome'
      ? `La suite de ${suggestion.seedTitle}`
      : `Parce que vous avez aimé ${suggestion.seedTitle}`;
  }

  showBroaderSuggestions(): void {
    this.minimumHeartsChange.emit(2);
  }
}
