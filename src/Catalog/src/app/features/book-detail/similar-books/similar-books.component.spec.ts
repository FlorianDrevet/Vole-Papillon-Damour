import {ComponentFixture, TestBed} from '@angular/core/testing';
import {RouterModule} from '@angular/router';
import {DesignSystemModule} from '@vpd/ui';
import {Subject, of, throwError} from 'rxjs';

import {CatalogApiService} from '../../../core/catalog-api.service';
import {
  CatalogSimilarBook,
  CatalogSimilarBooksResponse,
} from '../../../core/catalog.models';
import {BookCardComponent} from '../../../shared/book-card/book-card.component';
import {SimilarBooksComponent} from './similar-books.component';

describe('SimilarBooksComponent', () => {
  let fixture: ComponentFixture<SimilarBooksComponent>;
  let api: jasmine.SpyObj<CatalogApiService>;

  beforeEach(async () => {
    api = jasmine.createSpyObj<CatalogApiService>('CatalogApiService', ['getSimilarBooks']);
    await TestBed.configureTestingModule({
      declarations: [BookCardComponent, SimilarBooksComponent],
      imports: [RouterModule.forRoot([]), DesignSystemModule],
      providers: [{provide: CatalogApiService, useValue: api}],
    }).compileComponents();

    fixture = TestBed.createComponent(SimilarBooksComponent);
    fixture.componentRef.setInput('isbn13', '9782070408504');
  });

  it('shows an accessible loading skeleton while the request is pending', () => {
    api.getSimilarBooks.and.returnValue(new Subject<CatalogSimilarBooksResponse>());

    fixture.detectChanges();

    const loading = fixture.nativeElement.querySelector('[data-zone="similar-loading"]') as HTMLElement;
    expect(loading).not.toBeNull();
    expect(loading.getAttribute('aria-busy')).toBe('true');
  });

  it('renders five cards with the exact heading when enough neighbors are returned', () => {
    api.getSimilarBooks.and.returnValue(of({books: books(5)}));

    fixture.detectChanges();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('h2')?.textContent.trim())
      .toBe('Si ce livre vous a plu.');
    expect(fixture.nativeElement.querySelectorAll('app-book-card').length).toBe(5);
  });

  it('hides the whole section when fewer than two neighbors are returned', () => {
    api.getSimilarBooks.and.returnValue(of({books: books(1)}));

    fixture.detectChanges();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[data-zone="R1-section"]')).toBeNull();
    expect(fixture.nativeElement.querySelector('[role="alert"]')).toBeNull();
  });

  it('hides the section without an error message when the request fails', () => {
    api.getSimilarBooks.and.returnValue(throwError(() => new Error('offline')));

    fixture.detectChanges();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[data-zone="R1-section"]')).toBeNull();
    expect(fixture.nativeElement.querySelector('[role="alert"]')).toBeNull();
  });

  it('loads recommendations again when isbn13 changes', () => {
    api.getSimilarBooks.and.returnValues(
      of({books: books(2)}),
      of({books: books(3)}),
    );

    fixture.detectChanges();
    fixture.componentRef.setInput('isbn13', '9782070612758');
    fixture.detectChanges();

    expect(api.getSimilarBooks.calls.count()).toBe(2);
    expect(api.getSimilarBooks.calls.argsFor(0)[0]).toBe('9782070408504');
    expect(api.getSimilarBooks.calls.argsFor(1)[0]).toBe('9782070612758');
  });
});

function books(count: number): CatalogSimilarBook[] {
  return Array.from({length: count}, (_, index) => ({
    isbn13: `978207040850${index}`,
    title: `Livre ${index + 1}`,
    authors: 'Autrice',
    publisher: 'Éditeur',
    publicationYear: 2025,
    physicalFormat: 'Poche',
    language: 'fr',
    genre: 'Roman',
    workId: null,
    coverUrl: null,
    quantityAvailable: 1,
    quantityAnnounced: 0,
    nextFairAt: null,
    lastAvailableAt: '2026-09-10T10:00:00Z',
    firstSeenAt: '2026-09-10T10:00:00Z',
    updatedAt: '2026-09-10T10:00:00Z',
    isRare: false,
    reason: 'Theme',
  }));
}
