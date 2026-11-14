import {ChangeDetectionStrategy, Component, EventEmitter, Input, Output} from '@angular/core';

import {LocalFavoriteEntry} from '../../core/favorites/local-favorites.store';

@Component({
  selector: 'app-favorite-list',
  standalone: false,
  templateUrl: './favorite-list.component.html',
  styleUrls: ['./favorite-list.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FavoriteListComponent {
  @Input() entries: LocalFavoriteEntry[] = [];
  @Input() computingByIsbn: Readonly<Record<string, boolean>> = {};
  @Input() maximum = 50;
  @Output() readonly addRequested = new EventEmitter<void>();
  @Output() readonly removeRequested = new EventEmitter<LocalFavoriteEntry>();

  availabilityLabel(entry: LocalFavoriteEntry): string | null {
    if ((entry.quantityAvailable ?? 0) > 0) {
      return `${entry.quantityAvailable} dispo. à la bourse`;
    }
    if ((entry.quantityAnnounced ?? 0) > 0) {
      return `${entry.quantityAnnounced} annoncée${entry.quantityAnnounced === 1 ? '' : 's'} à la bourse`;
    }
    return null;
  }

  remove(entry: LocalFavoriteEntry): void {
    this.removeRequested.emit(entry);
  }
}
