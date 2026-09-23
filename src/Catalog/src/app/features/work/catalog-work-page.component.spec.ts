import {provideZonelessChangeDetection, signal} from '@angular/core';
import {ComponentFixture, TestBed} from '@angular/core/testing';
import {ActivatedRoute, convertToParamMap, RouterModule} from '@angular/router';
import {By} from '@angular/platform-browser';
import {Meta, Title} from '@angular/platform-browser';
import {Subject, of} from 'rxjs';
import {DesignSystemModule} from '@vpd/ui';

import {CatalogApiService} from '../../core/catalog-api.service';
import {CatalogAuthService} from '../../core/catalog-auth.service';
import {CatalogSelectionService} from '../../core/selection/catalog-selection.service';
import {CatalogBook, CatalogWorkResponse} from '../../core/catalog.models';
import {BookCardComponent} from '../../shared/book-card/book-card.component';
import {CatalogAuthPromptComponent} from '../../shared/components/auth-prompt/catalog-auth-prompt.component';
import {SelectionButtonComponent} from '../../shared/components/selection-button/selection-button.component';
import {CatalogWorkPageComponent} from './catalog-work-page.component';

describe('CatalogWorkPageComponent', () => {
  let fixture: ComponentFixture<CatalogWorkPageComponent>;
  let api: jasmine.SpyObj<CatalogApiService>;
  let auth: jasmine.SpyObj<CatalogAuthService>;
  let selection: jasmine.SpyObj<CatalogSelectionService>;
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

  const secondEdition: CatalogBook = {
    ...edition,
    isbn13: '9782070612758',
    title: 'Petit Ours brun découvre les saisons',
    quantityAvailable: 0,
    quantityAnnounced: 2,
  };

  const response: CatalogWorkResponse = {
    workId: 'work-42',
    title: 'Petit Ours brun',
    authors: 'Aubinais, Marie, Bour, Danièle',
    editions: [edition, secondEdition],
  };

  beforeEach(async () => {
    response$ = new Subject<CatalogWorkResponse>();
    api = jasmine.createSpyObj<CatalogApiService>('CatalogApiService', ['getWork']);
    api.getWork.and.returnValue(response$.asObservable());
    auth = jasmine.createSpyObj<CatalogAuthService>('CatalogAuthService', ['login']);
    auth.login.and.resolveTo();
    selection = jasmine.createSpyObj<CatalogSelectionService>(
      'CatalogSelectionService',
      ['add', 'remove'],
      {keys: signal<ReadonlySet<string>>(new Set()), mode: signal<'local' | 'synced' | 'local-unsynced'>('synced')},
    );
    selection.add.and.resolveTo();
    selection.remove.and.resolveTo();

    await TestBed.configureTestingModule({
      declarations: [CatalogWorkPageComponent, BookCardComponent, CatalogAuthPromptComponent, SelectionButtonComponent],
      imports: [RouterModule.forRoot([]), DesignSystemModule],
      providers: [
        provideZonelessChangeDetection(),
        {provide: CatalogApiService, useValue: api},
        {provide: CatalogAuthService, useValue: auth},
        {provide: CatalogSelectionService, useValue: selection},
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

  it('shows one selection action inside each edition row and no global work action', async () => {
    fixture.detectChanges();
    response$.next(response);
    await fixture.whenStable();
    fixture.detectChanges();

    const actions = fixture.debugElement.queryAll(By.directive(SelectionButtonComponent));
    expect(actions.length).toBe(response.editions.length);
    expect(actions.map(action => action.componentInstance.ref())).toEqual([
      {kind: 'edition', isbn13: edition.isbn13},
      {kind: 'edition', isbn13: secondEdition.isbn13},
    ]);
    expect(fixture.nativeElement.querySelector('.work-header app-selection-button')).toBeNull();
  });
});
