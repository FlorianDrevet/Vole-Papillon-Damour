import {ComponentFixture, TestBed} from '@angular/core/testing';
import {PLATFORM_ID, signal, WritableSignal} from '@angular/core';
import {of} from 'rxjs';

import {CatalogApiService} from '../../../core/catalog-api.service';
import {CatalogFavoritesService} from '../../../core/favorites/catalog-favorites.service';
import {LocalFavoriteEntry} from '../../../core/favorites/local-favorites.store';
import {
  CatalogBook,
  CatalogReferenceSearchResponse,
  CatalogSearchResponse,
} from '../../../core/catalog.models';
import {AddFavoritePanelComponent} from './add-favorite-panel.component';

describe('AddFavoritePanelComponent', () => {
  let fixture: ComponentFixture<AddFavoritePanelComponent>;
  let api: jasmine.SpyObj<CatalogApiService>;
  let favorites: jasmine.SpyObj<CatalogFavoritesService>;
  let favoriteList: WritableSignal<LocalFavoriteEntry[]>;
  let favoriteIsbns: WritableSignal<ReadonlySet<string>>;

  beforeEach(async () => {
    api = jasmine.createSpyObj<CatalogApiService>(
      'CatalogApiService',
      ['search', 'searchReferences', 'searchIsbndbEditions'],
    );
    api.search.and.returnValue(of(searchResponse([book()])));
    api.searchReferences.and.returnValue(of(referenceResponse()));
    api.searchIsbndbEditions.and.returnValue(of({items: [], total: 0, truncated: false, unavailable: null}));
    favoriteList = signal<LocalFavoriteEntry[]>([]);
    favoriteIsbns = signal<ReadonlySet<string>>(new Set());
    favorites = jasmine.createSpyObj<CatalogFavoritesService>(
      'CatalogFavoritesService',
      ['add', 'remove'],
      {
        mode: signal<'local' | 'remote'>('local'),
        favorites: favoriteList,
        favoriteIsbns,
      },
    );
    favorites.add.and.resolveTo(true);
    favorites.remove.and.resolveTo();

    await TestBed.configureTestingModule({
      declarations: [AddFavoritePanelComponent],
      providers: [
        {provide: PLATFORM_ID, useValue: 'browser'},
        {provide: CatalogApiService, useValue: api},
        {provide: CatalogFavoritesService, useValue: favorites},
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(AddFavoritePanelComponent);
    fixture.componentInstance.open = true;
    fixture.detectChanges();
  });

  it('does not call ISBNdb while typing or searching the free catalogue sources', async () => {
    const input = fixture.nativeElement.querySelector('[data-zone="E3-recherche"]') as HTMLInputElement;
    input.value = 'Le Seigneur des anneaux';
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    expect(api.search).not.toHaveBeenCalled();
    expect(api.searchReferences).not.toHaveBeenCalled();
    expect(api.searchIsbndbEditions).not.toHaveBeenCalled();

    await fixture.componentInstance.search();
    fixture.detectChanges();

    expect(api.search).toHaveBeenCalledTimes(1);
    expect(api.searchReferences).toHaveBeenCalledTimes(1);
    expect(api.searchIsbndbEditions).not.toHaveBeenCalled();
  });

  it('calls ISBNdb only when the reader asks for other editions', async () => {
    fixture.componentInstance.query = 'Le Petit Prince';
    await fixture.componentInstance.search();
    fixture.detectChanges();

    (fixture.nativeElement.querySelector(
      '[data-zone="E3-voir-autres-editions"] button',
    ) as HTMLButtonElement).click();
    await fixture.whenStable();

    expect(api.searchIsbndbEditions).toHaveBeenCalledOnceWith('Le Petit Prince');
  });

  it('adds a selected edition once and marks it as added', async () => {
    fixture.componentInstance.query = 'Le Petit Prince';
    await fixture.componentInstance.search();
    fixture.detectChanges();

    const button = fixture.nativeElement.querySelector(
      '[data-zone="E3-bouton-ajouter-edition"]',
    ) as HTMLButtonElement;
    button.click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(favorites.add).toHaveBeenCalledOnceWith({
      isbn13: '9782070612758',
      title: 'Le Petit Prince',
      authors: 'Antoine de Saint-Exupéry',
      coverUrl: null,
    });
    expect(button.getAttribute('aria-pressed')).toBe('true');
    expect(button.textContent).toContain('Ajouté');
  });

  it('shows the anonymous list limit and does not attempt to add a fifty-first favorite', async () => {
    const fiftyEntries = Array.from({length: 50}, (_, index) => ({
      isbn13: (index + 1).toString().padStart(13, '0'),
      title: `Livre ${index + 1}`,
      authors: null,
      coverUrl: null,
      addedAt: '2026-11-14T10:00:00Z',
    }));
    favoriteList.set(fiftyEntries);
    favoriteIsbns.set(new Set(fiftyEntries.map(entry => entry.isbn13)));
    fixture.componentInstance.query = 'Le Petit Prince';
    await fixture.componentInstance.search();
    fixture.detectChanges();

    (fixture.nativeElement.querySelector(
      '[data-zone="E3-bouton-ajouter-edition"]',
    ) as HTMLButtonElement).click();
    fixture.detectChanges();

    const alert = fixture.nativeElement.querySelector('[data-zone="E11-plafond-liste"]') as HTMLElement;
    expect(alert.textContent).toContain('Vous avez atteint 50 coups de cœur.');
    expect(alert.textContent).toContain("créez un compte pour aller jusqu'à 200");
    expect(favorites.add).not.toHaveBeenCalled();
  });

  it('closes with Escape and traps Tab within the open panel', async () => {
    const closed = jasmine.createSpy('closed');
    fixture.componentInstance.closed.subscribe(closed);
    await fixture.whenStable();
    fixture.detectChanges();
    const focusable = fixture.nativeElement.querySelectorAll(
      'button:not([disabled]), input:not([disabled])',
    ) as NodeListOf<HTMLElement>;

    focusable[focusable.length - 1].focus();
    document.dispatchEvent(new KeyboardEvent('keydown', {key: 'Tab'}));
    expect(document.activeElement).toBe(focusable[0]);

    document.dispatchEvent(new KeyboardEvent('keydown', {key: 'Escape'}));
    expect(closed).toHaveBeenCalledTimes(1);
  });

  function book(isbn13 = '9782070612758'): CatalogBook {
    return {
      isbn13,
      title: 'Le Petit Prince',
      authors: 'Antoine de Saint-Exupéry',
      publisher: 'Gallimard',
      publicationYear: 1999,
      physicalFormat: 'Poche',
      language: 'fr',
      genre: 'Jeunesse',
      workId: 'OL42W',
      coverUrl: null,
      quantityAvailable: 1,
      quantityAnnounced: 0,
      nextFairAt: null,
      lastAvailableAt: null,
      firstSeenAt: '2026-10-01T10:00:00Z',
      updatedAt: '2026-11-14T10:00:00Z',
      isRare: false,
    };
  }

  function searchResponse(books: CatalogBook[]): CatalogSearchResponse {
    return {
      generatedAt: '2026-11-14T10:00:00Z',
      books,
      totalCount: books.length,
      hasMore: false,
      page: 1,
      pageSize: 20,
      genres: [],
    };
  }

  function referenceResponse(): CatalogReferenceSearchResponse {
    return {
      generatedAt: '2026-11-14T10:00:00Z',
      query: 'Le Petit Prince',
      items: [],
      page: 1,
      pageSize: 20,
    };
  }
});
