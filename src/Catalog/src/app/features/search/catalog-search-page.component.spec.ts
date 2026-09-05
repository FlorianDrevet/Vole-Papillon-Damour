import {provideZonelessChangeDetection} from '@angular/core';
import {ComponentFixture, TestBed} from '@angular/core/testing';
import {FormsModule} from '@angular/forms';
import {ActivatedRoute, convertToParamMap, RouterModule} from '@angular/router';
import {Subject, of} from 'rxjs';

import {CatalogApiService} from '../../core/catalog-api.service';
import {CatalogSearchResponse} from '../../core/catalog.models';
import {BookCardComponent} from '../../shared/book-card/book-card.component';
import {CatalogSearchPageComponent} from './catalog-search-page.component';

describe('CatalogSearchPageComponent', () => {
  let fixture: ComponentFixture<CatalogSearchPageComponent>;
  let api: jasmine.SpyObj<CatalogApiService>;
  let response$: Subject<CatalogSearchResponse>;

  const response: CatalogSearchResponse = {
    generatedAt: '2026-09-05T06:00:00Z',
    books: [{
      isbn13: '9791036377426',
      title: 'Petit Ours brun se promène en forêt',
      authors: 'Aubinais, Marie, Bour, Danièle',
      publisher: 'Bayard jeunesse',
      publicationYear: 2025,
      physicalFormat: null,
      language: 'fr',
      genre: 'Jeunesse',
      workId: null,
      coverUrl: null,
      quantityAvailable: 3,
      quantityAnnounced: 0,
      nextFairAt: null,
      lastAvailableAt: '2026-09-05T06:00:00Z',
      firstSeenAt: '2026-09-05T06:00:00Z',
      updatedAt: '2026-09-05T06:00:00Z',
      isRare: false,
    }],
    totalCount: 1,
    page: 1,
    pageSize: 24,
    genres: ['Jeunesse'],
  };

  beforeEach(async () => {
    response$ = new Subject<CatalogSearchResponse>();
    api = jasmine.createSpyObj<CatalogApiService>('CatalogApiService', ['search']);
    api.search.and.returnValue(response$.asObservable());

    await TestBed.configureTestingModule({
      declarations: [CatalogSearchPageComponent, BookCardComponent],
      imports: [FormsModule, RouterModule.forRoot([])],
      providers: [
        provideZonelessChangeDetection(),
        {provide: CatalogApiService, useValue: api},
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              data: {browse: false},
              queryParamMap: convertToParamMap({}),
            },
            queryParamMap: of(convertToParamMap({})),
          },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(CatalogSearchPageComponent);
  });

  it('renders an asynchronous catalog response in zoneless mode', async () => {
    fixture.detectChanges();

    response$.next(response);
    await fixture.whenStable();

    expect(fixture.nativeElement.textContent).toContain('Petit Ours brun se promène en forêt');
    expect(fixture.nativeElement.textContent).not.toContain('Le catalogue arrive…');
  });
});
