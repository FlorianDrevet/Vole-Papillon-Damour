import {ComponentFixture, TestBed} from '@angular/core/testing';
import {RouterModule} from '@angular/router';
import {DesignSystemModule} from '@vpd/ui';

import {CatalogBook} from '../../core/catalog.models';
import {BookCardComponent} from './book-card.component';

describe('BookCardComponent', () => {
  let fixture: ComponentFixture<BookCardComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [BookCardComponent],
      imports: [RouterModule.forRoot([]), DesignSystemModule],
    }).compileComponents();

    fixture = TestBed.createComponent(BookCardComponent);
  });

  it('keeps the available and announced quantities visibly separate', () => {
    const book: CatalogBook = {
      isbn13: '9782070408504',
      title: 'Le Petit Prince',
      authors: 'Antoine de Saint-Exupéry',
      publisher: 'Gallimard',
      publicationYear: 1999,
      physicalFormat: null,
      language: 'fr',
      genre: 'Jeunesse',
      workId: 'work-1',
      coverUrl: null,
      quantityAvailable: 3,
      quantityAnnounced: 2,
      nextFairAt: '2026-09-14T09:30:00+02:00',
      lastAvailableAt: '2026-09-03T10:00:00Z',
      firstSeenAt: '2026-09-03T10:00:00Z',
      updatedAt: '2026-09-04T10:00:00Z',
      isRare: false,
    };
    fixture.componentInstance.book = book;
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('3 disponibles');
    expect(text).toContain('2 à partir du');
    expect(fixture.nativeElement.querySelector('a')?.getAttribute('href'))
      .toContain('/livres/le-petit-prince-antoine-de-saint-exupery-9782070408504');
  });

  it('labels a book without either quantity as exhausted', () => {
    fixture.componentInstance.book = {
      isbn13: '9782070363735',
      title: 'Un livre épuisé',
      authors: 'Une autrice',
      publisher: null,
      publicationYear: null,
      physicalFormat: null,
      language: null,
      genre: null,
      workId: null,
      coverUrl: null,
      quantityAvailable: 0,
      quantityAnnounced: 0,
      nextFairAt: null,
      lastAvailableAt: null,
      firstSeenAt: '2026-09-03T10:00:00Z',
      updatedAt: '2026-09-04T10:00:00Z',
      isRare: false,
    };
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Épuisé');
  });

  it('shows the quantity and date when a book is announced without stock', () => {
    fixture.componentInstance.book = {
      isbn13: '9782070363735',
      title: 'Un livre annoncé',
      authors: 'Une autrice',
      publisher: null,
      publicationYear: null,
      physicalFormat: null,
      language: null,
      genre: null,
      workId: null,
      coverUrl: null,
      quantityAvailable: 0,
      quantityAnnounced: 2,
      nextFairAt: '2026-09-14T09:30:00+02:00',
      lastAvailableAt: null,
      firstSeenAt: '2026-09-03T10:00:00Z',
      updatedAt: '2026-09-04T10:00:00Z',
      isRare: false,
    };
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Annoncé prochainement');
    expect(text).toContain('2 à partir du');
  });

  it('renders the generic book cover when no cover URL is available', () => {
    fixture.componentInstance.book = {
      isbn13: '9782070363735',
      title: 'Un livre sans image',
      authors: null,
      publisher: null,
      publicationYear: null,
      physicalFormat: null,
      language: null,
      genre: null,
      workId: null,
      coverUrl: null,
      quantityAvailable: 0,
      quantityAnnounced: 0,
      nextFairAt: null,
      lastAvailableAt: null,
      firstSeenAt: '2026-09-03T10:00:00Z',
      updatedAt: '2026-09-04T10:00:00Z',
      isRare: false,
    };
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('vpd-book-cover-placeholder')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('.cover-frame span')).toBeNull();
  });

  it('switches to the generic book cover when the external image fails', () => {
    fixture.componentInstance.book = {
      isbn13: '9782070363735',
      title: 'Un livre avec une image indisponible',
      authors: null,
      publisher: null,
      publicationYear: null,
      physicalFormat: null,
      language: null,
      genre: null,
      workId: null,
      coverUrl: 'https://covers.example.test/book.jpg',
      quantityAvailable: 0,
      quantityAnnounced: 0,
      nextFairAt: null,
      lastAvailableAt: null,
      firstSeenAt: '2026-09-03T10:00:00Z',
      updatedAt: '2026-09-04T10:00:00Z',
      isRare: false,
    };
    fixture.detectChanges();
    (fixture.nativeElement.querySelector('.cover-frame img') as HTMLImageElement)
      .dispatchEvent(new Event('error'));
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('vpd-book-cover-placeholder')).not.toBeNull();
  });
});
