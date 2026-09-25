import {ChangeDetectionStrategy, Component, Input} from '@angular/core';

import {
  CatalogBook,
  CatalogNeighborReason,
  neighborReasonLabels,
} from '../../core/catalog.models';
import {publicBookPath} from '../catalog-url';
import {catalogCoverUrl} from '../catalog-cover-url';

export type BookCardVariant = 'default' | 'grid' | 'list' | 'home';

@Component({
  selector: 'app-book-card',
  standalone: false,
  templateUrl: './book-card.component.html',
  styleUrls: ['./book-card.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BookCardComponent {
  @Input({required: true}) book!: CatalogBook;
  @Input() variant: BookCardVariant = 'grid';
  @Input() reason: CatalogNeighborReason | null = null;

  coverFailed = false;

  bookPath(): string {
    return publicBookPath(this.book);
  }

  displayCoverUrl(): string | null {
    return catalogCoverUrl(this.book.coverUrl);
  }

  availabilityLabel(): string {
    if (this.book.quantityAvailable > 0) {
      return `${this.book.quantityAvailable} ${this.book.quantityAvailable === 1 ? 'disponible' : 'disponibles'}`;
    }
    return this.book.quantityAnnounced > 0 ? 'Annoncé prochainement' : 'Épuisé';
  }

  announcedLabel(): string {
    if (this.book.quantityAnnounced <= 0) {
      return '';
    }

    const quantity = `${this.book.quantityAnnounced}`;
    return this.book.nextFairAt
      ? `${quantity} à partir du ${this.formatShortDate(this.book.nextFairAt)}`
      : `${quantity} prochainement, date à préciser`;
  }

  homeStatusLabel(): string {
    if (this.book.quantityAvailable > 0) {
      return this.book.quantityAvailable + ' dispo.';
    }

    if (this.book.quantityAnnounced > 0) {
      return this.book.quantityAnnounced + ' à venir';
    }

    return 'Parti';
  }

  homeActionLabel(): string {
    return this.book.quantityAvailable > 0 || this.book.quantityAnnounced > 0
      ? 'Suivre'
      : "M'alerter";
  }

  reasonLabel(): string {
    return this.reason ? neighborReasonLabels[this.reason] : '';
  }

  reasonClass(): string {
    return this.reason
      ? `reason-${this.reason.replace(/[A-Z]/g, letter => `-${letter.toLowerCase()}`).replace(/^-/, '')}`
      : '';
  }

  formatShortDate(value: string): string {
    return new Intl.DateTimeFormat('fr-FR', {
      day: 'numeric',
      month: 'short',
      timeZone: 'Europe/Paris',
    }).format(new Date(value)).replace('.', '');
  }

  onCoverError(): void {
    this.coverFailed = true;
  }
}
