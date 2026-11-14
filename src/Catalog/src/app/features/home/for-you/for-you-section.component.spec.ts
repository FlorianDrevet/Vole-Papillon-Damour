import {ComponentFixture, TestBed} from '@angular/core/testing';
import {signal, WritableSignal} from '@angular/core';
import {RouterModule} from '@angular/router';
import type {AccountInfo} from '@azure/msal-browser';
import {DesignSystemModule} from '@vpd/ui';

import {CatalogAuthService} from '../../../core/catalog-auth.service';
import {CatalogFavoriteSuggestion} from '../../../core/catalog.models';
import {CatalogFavoritesService} from '../../../core/favorites/catalog-favorites.service';
import {LocalFavoriteEntry, LocalFavoritesStore} from '../../../core/favorites/local-favorites.store';
import {BookCardComponent} from '../../../shared/book-card/book-card.component';
import {HeartsComponent} from '../../../shared/components/hearts/hearts.component';
import {ForYouSectionComponent} from './for-you-section.component';

describe('ForYouSectionComponent', () => {
  let fixture: ComponentFixture<ForYouSectionComponent>;
  let auth: jasmine.SpyObj<CatalogAuthService>;
  let favoriteSuggestions: WritableSignal<CatalogFavoriteSuggestion[]>;
  let favoriteEntries: WritableSignal<LocalFavoriteEntry[]>;
  let suggestionStatus: WritableSignal<string>;
  let minimumHearts: WritableSignal<number>;
  let favorites: CatalogFavoritesService;
  let localStore: jasmine.SpyObj<LocalFavoritesStore>;
  let accountState: WritableSignal<AccountInfo | null>;
  let authenticated: WritableSignal<boolean>;
  let homeEntryDismissed: boolean;

  beforeEach(async () => {
    homeEntryDismissed = false;
    accountState = signal<AccountInfo | null>(null);
    authenticated = signal(false);
    auth = {
      account: accountState,
      isAuthenticated: authenticated,
      initialize: jasmine.createSpy('initialize').and.resolveTo(),
    } as unknown as jasmine.SpyObj<CatalogAuthService>;

    favoriteSuggestions = signal<CatalogFavoriteSuggestion[]>([]);
    favoriteEntries = signal<LocalFavoriteEntry[]>([]);
    suggestionStatus = signal('NoFavorites');
    minimumHearts = signal(3);
    favorites = {
      suggestions: favoriteSuggestions.asReadonly(),
      favorites: favoriteEntries.asReadonly(),
      suggestionStatus: suggestionStatus.asReadonly(),
      currentMinimumHearts: minimumHearts.asReadonly(),
      refresh: jasmine.createSpy('refresh').and.resolveTo(),
      onSignedIn: jasmine.createSpy('onSignedIn').and.resolveTo(),
    } as unknown as CatalogFavoritesService;

    localStore = jasmine.createSpyObj<LocalFavoritesStore>(
      'LocalFavoritesStore',
      ['homeEntryDismissed', 'setHomeEntryDismissed'],
    );
    localStore.homeEntryDismissed.and.callFake(() => homeEntryDismissed);
    localStore.setHomeEntryDismissed.and.callFake(value => {
      homeEntryDismissed = value;
    });

    await TestBed.configureTestingModule({
      declarations: [ForYouSectionComponent, BookCardComponent, HeartsComponent],
      imports: [RouterModule.forRoot([]), DesignSystemModule],
      providers: [
        {provide: CatalogAuthService, useValue: auth},
        {provide: CatalogFavoritesService, useValue: favorites},
        {provide: LocalFavoritesStore, useValue: localStore},
      ],
    }).compileComponents();

    fixture = createFixture();
  });

  it('shows the entry for a member with purchases but no favorites', async () => {
    signIn();
    fixture.detectChanges();
    await waitForLoad();
    fixture.detectChanges();

    expect(auth.initialize).toHaveBeenCalled();
    expect(favorites.onSignedIn).toHaveBeenCalledOnceWith();
    expect(fixture.nativeElement.querySelector('[data-zone="E11-encart-entree"]')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('[data-zone="E1-section-pour-vous"]')).toBeNull();
  });

  it('shows at least three favorite suggestions with hearts and their reason', async () => {
    signIn();
    favoriteEntries.set(localFavorites());
    favoriteSuggestions.set(suggestions());
    suggestionStatus.set('Enabled');
    fixture.detectChanges();
    await waitForLoad();
    fixture.detectChanges();

    const section = fixture.nativeElement.querySelector('[data-zone="E1-section-pour-vous"]') as HTMLElement;
    expect(section).not.toBeNull();
    expect(section.querySelector('.for-you-eyebrow')?.textContent.trim()).toBe('Pour vous, Camille');
    expect(section.querySelector('h2')?.textContent.trim()).toBe("D'après vos coups de cœur.");
    expect(section.querySelectorAll('[data-zone="E1-carte"]')).toHaveSize(4);
    expect(section.querySelector('[aria-label="5 cœurs sur 5"]')).not.toBeNull();
    expect(section.querySelector('[data-zone="E1-raison"]')?.textContent.trim())
      .toBe("La suite de Les hommes qui n'aimaient pas les femmes");
  });

  it('uses a horizontal 156px card track on mobile and links to all suggestions', async () => {
    favoriteEntries.set(localFavorites());
    favoriteSuggestions.set(suggestions());
    suggestionStatus.set('Enabled');
    fixture.detectChanges();
    await waitForLoad();
    fixture.detectChanges();

    const track = fixture.nativeElement.querySelector('[data-zone="E1-defilement"]') as HTMLElement;
    expect(track).not.toBeNull();
    expect(track.classList).toContain('for-you-list');
    expect(fixture.nativeElement.querySelector('[data-zone="E1-lien-toutes"]')?.getAttribute('href'))
      .toBe('/trouver-mon-prochain-livre');
    expect(favorites.refresh).toHaveBeenCalledOnceWith();
  });

  it('shows the entry when fewer than three suggestions meet the selected threshold', async () => {
    favoriteEntries.set(localFavorites());
    favoriteSuggestions.set(suggestions().map((suggestion, index) => ({
      ...suggestion,
      hearts: index < 2 ? 2 : 3,
    })));
    minimumHearts.set(4);
    suggestionStatus.set('Enabled');

    fixture.detectChanges();
    await waitForLoad();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[data-zone="E11-encart-entree"]')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('[data-zone="E1-section-pour-vous"]')).toBeNull();
  });

  it('persists the choice to hide the entry across component instances', async () => {
    fixture.detectChanges();
    await waitForLoad();
    fixture.detectChanges();

    const hideButton = fixture.nativeElement.querySelector('[data-zone="E11-ne-plus-afficher"]') as HTMLButtonElement;
    expect(hideButton).not.toBeNull();
    hideButton.click();
    fixture.detectChanges();
    expect(localStore.setHomeEntryDismissed).toHaveBeenCalledOnceWith(true);
    expect(fixture.nativeElement.querySelector('[data-zone="E11-encart-entree"]')).toBeNull();

    fixture.destroy();
    fixture = createFixture();
    fixture.detectChanges();
    await waitForLoad();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[data-zone="E11-encart-entree"]')).toBeNull();
  });

  it('does not render the row or entry when suggestions are disabled', async () => {
    favoriteEntries.set(localFavorites());
    favoriteSuggestions.set(suggestions());
    suggestionStatus.set('Disabled');
    fixture.detectChanges();
    await waitForLoad();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[data-zone="E1-section-pour-vous"]')).toBeNull();
    expect(fixture.nativeElement.querySelector('[data-zone="E11-encart-entree"]')).toBeNull();
  });

  async function waitForLoad(): Promise<void> {
    await fixture.whenStable();
    await new Promise<void>(resolve => setTimeout(resolve, 0));
    await fixture.whenStable();
  }

  function createFixture(): ComponentFixture<ForYouSectionComponent> {
    return TestBed.createComponent(ForYouSectionComponent);
  }

  function signIn(): void {
    accountState.set({
      homeAccountId: 'member-id',
      environment: 'volepapillondamour.ciamlogin.com',
      tenantId: 'tenant-id',
      username: 'camille@example.test',
      localAccountId: 'local-member-id',
      name: 'Camille Dupont',
      idTokenClaims: {given_name: 'Camille'},
    });
    authenticated.set(true);
  }
});

function localFavorites(): LocalFavoriteEntry[] {
  return ['9782070612758', '9782266000001', '9782266000002'].map((isbn13, index) => ({
    isbn13,
    title: `Coup de cœur ${index + 1}`,
    authors: 'Autrice',
    coverUrl: null,
    addedAt: `2026-11-0${index + 1}T10:00:00Z`,
  }));
}

function suggestions(): CatalogFavoriteSuggestion[] {
  return [
    suggestion('9782070612758', 'NextTome', "Les hommes qui n'aimaient pas les femmes", 5),
    suggestion('9782266000001', 'Theme', 'Dune', 4),
    suggestion('9782266000002', 'SameAuthor', 'Bel-Ami', 3),
    suggestion('9782266000003', 'SameSeries', 'Les Trois Mousquetaires', 4),
  ];
}

function suggestion(
  isbn13: string,
  reason: CatalogFavoriteSuggestion['reason'],
  seedTitle: string,
  hearts: number,
): CatalogFavoriteSuggestion {
  return {
    isbn13,
    title: `Livre ${isbn13.slice(-1)}`,
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
    firstSeenAt: '2026-09-01T10:00:00Z',
    updatedAt: '2026-09-10T10:00:00Z',
    isRare: false,
    reason,
    seedTitle,
    hearts,
  };
}
