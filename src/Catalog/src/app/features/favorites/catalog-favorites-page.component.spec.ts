import {ComponentFixture, TestBed} from '@angular/core/testing';
import {NO_ERRORS_SCHEMA, signal, WritableSignal} from '@angular/core';
import {RouterModule} from '@angular/router';

import {CatalogAuthService} from '../../core/catalog-auth.service';
import {CatalogFavoritesService} from '../../core/favorites/catalog-favorites.service';
import {LocalFavoriteEntry, LocalFavoritesStore} from '../../core/favorites/local-favorites.store';
import {CatalogFavoriteSuggestion} from '../../core/catalog.models';
import {CatalogFavoritesPageComponent} from './catalog-favorites-page.component';
import {FavoriteListComponent} from './favorite-list.component';
import {SuggestionGridComponent} from './suggestion-grid.component';
import {BrowserStorageBannerComponent} from './browser-storage-banner.component';
import {MinimumHeartsControlComponent} from './minimum-hearts-control.component';
import {BookCardComponent} from '../../shared/book-card/book-card.component';
import {CatalogCoverDirective} from '../../shared/catalog-cover.directive';
import {HeartsComponent} from '../../shared/components/hearts/hearts.component';
import {DesignSystemModule} from '@vpd/ui';

describe('CatalogFavoritesPageComponent', () => {
  let fixture: ComponentFixture<CatalogFavoritesPageComponent>;
  let auth: jasmine.SpyObj<CatalogAuthService> & {isAuthenticated: WritableSignal<boolean>};
  let favorites: jasmine.SpyObj<CatalogFavoritesService> & {
    mode: WritableSignal<'local' | 'remote'>;
    favorites: WritableSignal<LocalFavoriteEntry[]>;
    suggestions: WritableSignal<CatalogFavoriteSuggestion[]>;
    computingByIsbn: WritableSignal<Readonly<Record<string, boolean>>>;
    suggestionStatus: WritableSignal<string>;
    currentMinimumHearts: WritableSignal<number>;
    fairDigestConsent: WritableSignal<boolean>;
  };
  let localStore: jasmine.SpyObj<LocalFavoritesStore> & {available: boolean};

  beforeEach(async () => {
    auth = Object.assign(jasmine.createSpyObj<CatalogAuthService>('CatalogAuthService', ['initialize']), {
      isAuthenticated: signal(false),
    });
    auth.initialize.and.resolveTo();
    favorites = Object.assign(jasmine.createSpyObj<CatalogFavoritesService>(
      'CatalogFavoritesService',
      ['refresh', 'onSignedIn', 'remove', 'undoRemoval', 'setMinimumHearts', 'setFairDigestConsent'],
    ), {
      mode: signal<'local' | 'remote'>('local'),
      favorites: signal<LocalFavoriteEntry[]>([]),
      suggestions: signal<CatalogFavoriteSuggestion[]>([]),
      computingByIsbn: signal<Readonly<Record<string, boolean>>>({}),
      suggestionStatus: signal('Enabled'),
      currentMinimumHearts: signal(3),
      fairDigestConsent: signal(false),
    });
    localStore = Object.assign(jasmine.createSpyObj<LocalFavoritesStore>(
      'LocalFavoritesStore', ['bannerCollapsed', 'setBannerCollapsed'],
    ), {available: true});
    localStore.bannerCollapsed.and.returnValue(false);
    favorites.refresh.and.resolveTo();
    favorites.onSignedIn.and.resolveTo();
    favorites.remove.and.resolveTo();
    favorites.undoRemoval.and.returnValue(true);
    favorites.setMinimumHearts.and.callFake(async value => favorites.currentMinimumHearts.set(value));
    favorites.setFairDigestConsent.and.resolveTo();

    await TestBed.configureTestingModule({
      declarations: [
        CatalogFavoritesPageComponent,
        FavoriteListComponent,
        SuggestionGridComponent,
        BrowserStorageBannerComponent,
        MinimumHeartsControlComponent,
        BookCardComponent,
        CatalogCoverDirective,
        HeartsComponent,
      ],
      imports: [RouterModule.forRoot([]), DesignSystemModule],
      schemas: [NO_ERRORS_SCHEMA],
      providers: [
        {provide: CatalogAuthService, useValue: auth},
        {provide: CatalogFavoritesService, useValue: favorites},
        {provide: LocalFavoritesStore, useValue: localStore},
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(CatalogFavoritesPageComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  });

  it('shows the browser-storage banner as soon as an anonymous reader has a favorite', () => {
    favorites.favorites.set([favorite()]);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[data-zone="E2-bandeau-navigateur"]'))
      .not.toBeNull();
    expect(fixture.nativeElement.textContent)
      .toContain('Vos coups de cœur sont enregistrés dans ce navigateur.');
  });

  it('shows the fair email preference to a member instead of the browser-storage banner', () => {
    auth.isAuthenticated.set(true);
    favorites.mode.set('remote');
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[data-zone="E2-preference-email"]'))
      .not.toBeNull();
    expect(fixture.nativeElement.querySelector('[data-zone="E2-bandeau-navigateur"]'))
      .toBeNull();
    expect(fixture.nativeElement.textContent)
      .toContain('Recevoir avant chaque bourse une sélection de livres qui pourraient me plaire');
  });

  it('saves a member email preference change', async () => {
    auth.isAuthenticated.set(true);
    favorites.mode.set('remote');
    fixture.detectChanges();

    const checkbox = fixture.nativeElement.querySelector('#fair-digest-consent') as HTMLInputElement;
    checkbox.click();
    await fixture.whenStable();

    expect(favorites.setFairDigestConsent).toHaveBeenCalledOnceWith(true);
  });

  it('announces when browser storage cannot keep favorites', () => {
    localStore.available = false;
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[data-zone="E11-stockage-indisponible"]'))
      .not.toBeNull();
  });

  it('filters every suggestion below the selected four-heart threshold', async () => {
    favorites.favorites.set([favorite()]);
    favorites.suggestions.set([suggestion('1111111111111', 4), suggestion('2222222222222', 3)]);
    fixture.detectChanges();

    const threshold = fixture.nativeElement.querySelector('[data-zone="E2-exigence-4"]') as HTMLButtonElement;
    threshold.click();
    await fixture.whenStable();
    fixture.detectChanges();

    const cards = fixture.nativeElement.querySelectorAll('[data-zone="E2-carte-suggestion"]');
    expect(cards.length).toBe(1);
    expect(cards[0].getAttribute('data-isbn')).toBe('1111111111111');
    expect(favorites.setMinimumHearts).toHaveBeenCalledOnceWith(4);
  });

  it('renders the E11 empty state when there are no favorites', () => {
    expect(fixture.nativeElement.querySelector('[data-zone="E11-e2-vide"]')).not.toBeNull();
    expect(fixture.nativeElement.textContent).toContain('Quel livre avez-vous adoré');
  });

  it('uses the mobile headline copy from the E2 mobile mockup', () => {
    expect(fixture.nativeElement.querySelector('.mobile-title')?.textContent)
      .toContain('Dites-nous ce que vous avez aimé');
  });

  it('announces recommendation work for a favorite that is still computing', () => {
    favorites.favorites.set([favorite()]);
    favorites.computingByIsbn.set({'9782070612758': true});
    fixture.detectChanges();

    const status = fixture.nativeElement.querySelector('[data-zone="E2-calcul-en-cours"]') as HTMLElement;
    expect(status).not.toBeNull();
    expect(status.textContent).toContain('Suggestions en cours de calcul…');
  });

  function favorite(): LocalFavoriteEntry {
    return {
      isbn13: '9782070612758',
      title: 'Le Petit Prince',
      authors: 'Antoine de Saint-Exupéry',
      coverUrl: null,
      addedAt: '2026-11-14T10:00:00Z',
    };
  }

  function suggestion(isbn13: string, hearts: number): CatalogFavoriteSuggestion {
    return {
      isbn13,
      title: `Livre ${hearts}`,
      authors: 'Camille Exemple',
      publisher: null,
      publicationYear: 2026,
      physicalFormat: null,
      language: 'fr',
      genre: 'Roman',
      workId: null,
      coverUrl: null,
      quantityAvailable: 1,
      quantityAnnounced: 0,
      nextFairAt: null,
      lastAvailableAt: null,
      firstSeenAt: '2026-11-14T10:00:00Z',
      updatedAt: '2026-11-14T10:00:00Z',
      isRare: false,
      reason: 'SameAuthor',
      seedTitle: 'Le Petit Prince',
      hearts,
    };
  }
});
