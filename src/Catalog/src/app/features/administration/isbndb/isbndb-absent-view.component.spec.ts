import {provideZonelessChangeDetection} from '@angular/core';
import {ComponentFixture, TestBed} from '@angular/core/testing';
import {FormsModule} from '@angular/forms';
import {of} from 'rxjs';

import {CatalogAdminApiService} from '../../../core/catalog-admin-api.service';
import {CatalogAdminIsbndbAbsentBooksPage} from '../../../core/catalog.models';
import {IsbndbAbsentViewComponent} from './isbndb-absent-view.component';

describe('IsbndbAbsentViewComponent', () => {
  let fixture: ComponentFixture<IsbndbAbsentViewComponent>;
  let api: jasmine.SpyObj<CatalogAdminApiService>;

  const page: CatalogAdminIsbndbAbsentBooksPage = {
    items: [{
      isbn13: '9782070409228',
      title: 'Les Misérables',
      authors: 'Victor Hugo',
      coverUrl: null,
      fallbackSource: 'Bnf',
      quantityAvailable: 3,
      missingFields: ['Summary', 'Cover', 'Pages'],
      summary: null,
      genre: null,
      publisher: null,
      publicationYear: null,
      pages: null,
      edition: null,
      genres: [],
      subjects: [],
      firstSeenAt: '2026-07-01T10:00:00Z',
      isbndbCheckedAt: '2026-07-01T10:00:00Z',
      isbndbCompletedAt: null,
    }],
    totalCount: 7,
    page: 1,
    pageSize: 5,
  };

  beforeEach(async () => {
    api = jasmine.createSpyObj<CatalogAdminApiService>('CatalogAdminApiService', ['getIsbndbAbsentBooks']);
    api.getIsbndbAbsentBooks.and.callFake((_token, filters = {}) => of({
      ...page,
      page: filters.page ?? 1,
      totalCount: filters.status === 'completed' ? 2 : 7,
    }));

    await TestBed.configureTestingModule({
      declarations: [IsbndbAbsentViewComponent],
      imports: [FormsModule],
      providers: [
        provideZonelessChangeDetection(),
        {provide: CatalogAdminApiService, useValue: api},
      ],
    }).compileComponents();
    fixture = TestBed.createComponent(IsbndbAbsentViewComponent);
    fixture.componentRef.setInput('accessToken', 'admin-token');
  });

  it('loads five incomplete books and marks missing fields and stock', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(api.getIsbndbAbsentBooks).toHaveBeenCalledWith('admin-token', jasmine.objectContaining({
      status: 'to-complete',
      page: 1,
      pageSize: 5,
    }));
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Livres hors ISBNdb.');
    expect(text).toContain('ISBNdb ne connaît pas ces livres');
    expect(text).toContain('Fiche de secours');
    expect(text).toContain('Résumé');
    expect(text).toContain('Couverture');
    expect(text).toContain('Pages');
    expect(text).toContain('En stock');
    expect(text).toContain('Compléter →');
    expect(fixture.nativeElement.querySelectorAll('[data-testid="isbndb-absent-book"]')).toHaveSize(1);
  });

  it('filters completed books, updates pagination, and emits the selected ISBN', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    (fixture.nativeElement.querySelector('[data-testid="isbndb-filter-completed"]') as HTMLButtonElement).click();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(api.getIsbndbAbsentBooks).toHaveBeenCalledWith('admin-token', jasmine.objectContaining({status: 'completed'}));
    expect(fixture.nativeElement.querySelector('.isbndb-absent-filters')?.textContent).toContain('Complétées · 2');

    const selected = jasmine.createSpy('bookSelected');
    fixture.componentInstance.bookSelected.subscribe(selected);
    (fixture.nativeElement.querySelector('[data-testid="isbndb-absent-open-book"]') as HTMLButtonElement).click();
    expect(selected).toHaveBeenCalledOnceWith('9782070409228');
  });

  it('searches by ISBN, title or author and requests five books per page', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    fixture.componentInstance.search = 'Camus';
    await fixture.componentInstance.submitSearch();
    await fixture.componentInstance.goToPage(2);

    expect(api.getIsbndbAbsentBooks).toHaveBeenCalledWith('admin-token', jasmine.objectContaining({
      status: 'to-complete', search: 'Camus', page: 1, pageSize: 5,
    }));
    expect(api.getIsbndbAbsentBooks).toHaveBeenCalledWith('admin-token', jasmine.objectContaining({
      status: 'to-complete', search: 'Camus', page: 2, pageSize: 5,
    }));
  });

  it('shows the specified empty state', async () => {
    api.getIsbndbAbsentBooks.and.returnValue(of({items: [], totalCount: 0, page: 1, pageSize: 5}));
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Aucun livre à compléter');
    expect((fixture.nativeElement as HTMLElement).textContent).toContain(
      'Les livres qu’ISBNdb ne connaît pas apparaîtront ici.',
    );
  });
});
