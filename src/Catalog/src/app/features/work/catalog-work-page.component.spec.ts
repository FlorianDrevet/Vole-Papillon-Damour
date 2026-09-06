import {provideZonelessChangeDetection} from '@angular/core';
import {ComponentFixture, TestBed} from '@angular/core/testing';
import {ActivatedRoute, convertToParamMap, RouterModule} from '@angular/router';
import {Meta, Title} from '@angular/platform-browser';
import {Subject, of} from 'rxjs';
import {DesignSystemModule} from '@vpd/ui';

import {CatalogApiService} from '../../core/catalog-api.service';
import {CatalogBook, CatalogWorkResponse} from '../../core/catalog.models';
import {BookCardComponent} from '../../shared/book-card/book-card.component';
import {CatalogWorkPageComponent} from './catalog-work-page.component';

describe('CatalogWorkPageComponent', () => {
  let fixture: ComponentFixture<CatalogWorkPageComponent>;
  let api: jasmine.SpyObj<CatalogApiService>;
  let response$: Subject<CatalogWorkResponse>;

  const edition: CatalogBook = {
    isbn13: '9791036377426',
    title: 'Petit Ours brun se promène en forêt',
    authors: 'Aubinais, Marie, Bour, Danièle',
    publisher: 'Bayard jeunesse',
    publicationYear: 2025,
    physicalFormat: null,
    language: 'fr',
    genre: 'Jeunesse',
    workId: 'work-42',
    coverUrl: null,
    quantityAvailable: 3,
    quantityAnnounced: 0,
    nextFairAt: null,
    lastAvailableAt: '2026-09-05T06:00:00Z',
    firstSeenAt: '2026-09-05T06:00:00Z',
    updatedAt: '2026-09-05T06:00:00Z',
    isRare: false,
  };

  const response: CatalogWorkResponse = {
    workId: 'work-42',
    title: 'Petit Ours brun',
    authors: 'Aubinais, Marie, Bour, Danièle',
    editions: [edition],
  };

  beforeEach(async () => {
    response$ = new Subject<CatalogWorkResponse>();
    api = jasmine.createSpyObj<CatalogApiService>('CatalogApiService', ['getWork']);
    api.getWork.and.returnValue(response$.asObservable());

    await TestBed.configureTestingModule({
      declarations: [CatalogWorkPageComponent, BookCardComponent],
      imports: [RouterModule.forRoot([]), DesignSystemModule],
      providers: [
        provideZonelessChangeDetection(),
        {provide: CatalogApiService, useValue: api},
        {provide: Title, useValue: jasmine.createSpyObj<Title>('Title', ['setTitle'])},
        {provide: Meta, useValue: jasmine.createSpyObj<Meta>('Meta', ['updateTag'])},
        {
          provide: ActivatedRoute,
          useValue: {
            paramMap: of(convertToParamMap({workId: 'work-42'})),
          },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(CatalogWorkPageComponent);
  });

  it('renders an asynchronous work response in zoneless mode', async () => {
    fixture.detectChanges();

    response$.next(response);
    await fixture.whenStable();

    expect(fixture.nativeElement.textContent).toContain('Petit Ours brun');
    expect(fixture.nativeElement.textContent).not.toContain("L'œuvre arrive…");
  });
});
