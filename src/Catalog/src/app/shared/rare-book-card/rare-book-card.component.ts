import {ChangeDetectionStrategy, Component, Input} from '@angular/core';

import {CatalogRareBook} from '../../core/catalog.models';

@Component({
  selector: 'app-rare-book-card',
  standalone: false,
  templateUrl: './rare-book-card.component.html',
  styleUrls: ['./rare-book-card.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CatalogRareBookCardComponent {
  @Input({required: true}) book!: CatalogRareBook;

  thumbnail(): string | null {
    return this.book.photos.find(photo => photo.position === 0)?.blobUri
      ?? this.book.photos[0]?.blobUri
      ?? null;
  }

  conditionLabel(): string {
    return ({
      AsNew: 'Comme neuf',
      GoodWithFlaws: 'Bon état avec défauts',
      Worn: 'Usé',
      Damaged: 'Abîmé',
    } as Record<string, string>)[this.book.condition] ?? this.book.condition;
  }

  technicalLine(): string {
    return [
      this.book.publisher,
      this.book.publicationYear?.toString(),
      this.book.binding,
      this.book.isbn13 ?? 'sans ISBN',
    ].filter(Boolean).join(' · ');
  }

  soldLabel(): string {
    if (!this.book.soldAt) {
      return 'Parti';
    }

    return `Vendu le ${new Intl.DateTimeFormat('fr-FR', {
      day: 'numeric',
      month: 'long',
      timeZone: 'Europe/Paris',
    }).format(new Date(this.book.soldAt))}`;
  }
}
