import {ComponentFixture, TestBed} from '@angular/core/testing';
import {FormsModule} from '@angular/forms';
import {RouterModule, Router} from '@angular/router';
import {of} from 'rxjs';

import {CatalogApiService} from '../../core/catalog-api.service';
import {CatalogBook, CatalogFair, CatalogSearchResponse} from '../../core/catalog.models';
import {BookCardComponent} from '../../shared/book-card/book-card.component';
import {DesignSystemModule} from '@vpd/ui';
import {CatalogHomePageComponent} from './catalog-home-page.component';

describe('CatalogHomePageComponent', () => {
  let fixture: ComponentFixture<CatalogHomePageComponent>;
  let api: jasmine.SpyObj<CatalogApiService>;
  let router: jasmine.SpyObj<Router>;

  const book: CatalogBook = {
    isbn13: '9782070612758',
    title: 'Le Petit Prince',
    authors: 'Antoine de Saint-Exupéry',
    publisher: 'Gallimard',
    publicationYear: 1999,
    physicalFormat: 'Poche',
    language: 'fr',
    genre: 'Jeunesse',
    workId: null,
    coverUrl: null,
    quantityAvailable: 3,
    quantityAnnounced: 0,
    nextFairAt: null,
    lastAvailableAt: '2026-09-04T10:00:00Z',
    firstSeenAt: '2026-09-04T10:00:00Z',
    updatedAt: '2026-09-04T10:00:00Z',
    isRare: false,
  };

  const searchResponse: CatalogSearchResponse = {
    generatedAt: '2026-09-06T08:00:00Z',
    books: [book],
    totalCount: 412,
    page: 1,
    pageSize: 3,
    genres: ['Jeunesse', 'Romans', 'Policier'],
  };

  const fair: CatalogFair = {
    id: 'fair-1',
    name: 'Bourse de mars',
    dateStart: '2026-03-14T00:00:00Z',
    dateEnd: '2026-03-15T00:00:00Z',
    openAt: '2026-03-14T09:30:00Z',
    closeAt: '2026-03-15T18:00:00Z',
    roadNumber: 46,
    city: 'Saint-Just-Saint-Rambert',
    cityCode: 42170,
    road: 'route de Saint-Marcellin',
  };

  beforeEach(async () => {
    api = jasmine.createSpyObj<CatalogApiService>('CatalogApiService', ['search', 'getNextFair']);
    api.search.and.returnValue(of(searchResponse));
    api.getNextFair.and.returnValue(of(fair));
    await TestBed.configureTestingModule({
      declarations: [CatalogHomePageComponent, BookCardComponent],
      imports: [FormsModule, RouterModule.forRoot([]), DesignSystemModule],
      providers: [
        {provide: CatalogApiService, useValue: api},
      ],
    }).compileComponents();

    router = TestBed.inject(Router) as jasmine.SpyObj<Router>;
    spyOn(router, 'navigate').and.resolveTo(true);

    fixture = TestBed.createComponent(CatalogHomePageComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  });

  it('puts the genre selector and availability count in the hero search', () => {
    expect(fixture.nativeElement.querySelector('.hero-genre-select')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('.hero-count')?.textContent).toContain('412');
    expect(fixture.nativeElement.querySelector('.home-account-callout')).not.toBeNull();
  });

  it('keeps the selected genre when sending a search from the hero', () => {
    fixture.componentInstance.search = 'petit prince';
    fixture.componentInstance.heroGenre = 'Jeunesse';

    fixture.componentInstance.submitSearch();

    expect(router.navigate).toHaveBeenCalledWith(['/recherche'], {
      queryParams: {q: 'petit prince', genre: 'Jeunesse'},
    });
  });
});
