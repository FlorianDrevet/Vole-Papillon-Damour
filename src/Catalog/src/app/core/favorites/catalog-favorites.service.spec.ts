import {TestBed} from '@angular/core/testing';
import {PLATFORM_ID, signal, WritableSignal} from '@angular/core';
import {of, throwError} from 'rxjs';

import {CatalogAuthService} from '../catalog-auth.service';
import {CatalogApiService} from '../catalog-api.service';
import {CatalogMemberApiService} from '../catalog-member-api.service';
import {
  CatalogMemberFavoritesResponse,
} from '../catalog.models';
import {CatalogFavoritesService} from './catalog-favorites.service';
import {LocalFavoritesStore} from './local-favorites.store';

describe('CatalogFavoritesService', () => {
  let api: jasmine.SpyObj<CatalogApiService>;
  let memberApi: jasmine.SpyObj<CatalogMemberApiService>;
  let auth: {isAuthenticated: WritableSignal<boolean>; getApiAccessToken: jasmine.Spy};
  let local: LocalFavoritesStore;

  beforeEach(() => {
    localStorage.removeItem('vpd.catalog.favorites.v1');
    api = jasmine.createSpyObj<CatalogApiService>(
      'CatalogApiService',
      ['getFavoriteSuggestions', 'declareAnonymousFavorite', 'withdrawAnonymousFavorite'],
    );
    memberApi = jasmine.createSpyObj<CatalogMemberApiService>(
      'CatalogMemberApiService',
      [
        'getFavorites', 'getRecommendations', 'getRecommendationPreference',
        'setRecommendationPreference', 'addFavorite', 'removeFavorite', 'mergeFavorites',
      ],
    );
    auth = {
      isAuthenticated: signal(false),
      getApiAccessToken: jasmine.createSpy('getApiAccessToken').and.resolveTo('member-token'),
    };
    api.getFavoriteSuggestions.and.returnValue(of({status: 'Enabled', minimumHearts: 3, items: [], seeds: []}));
    api.declareAnonymousFavorite.and.returnValue(of({isbn13: '9782070612758', count: 1}));
    api.withdrawAnonymousFavorite.and.returnValue(of({isbn13: '9782070612758', count: 0}));
    memberApi.getFavorites.and.returnValue(of(emptyFavorites()));
    memberApi.getRecommendations.and.returnValue(of({status: 'Enabled', minimumHearts: 3, items: []}));
    memberApi.getRecommendationPreference.and.returnValue(of({enabled: true, minimumHearts: 3}));
    memberApi.setRecommendationPreference.and.returnValue(of(void 0));
    memberApi.addFavorite.and.returnValue(of({isbn13: '9782070612758', alreadyPresent: false, total: 1}));
    memberApi.removeFavorite.and.returnValue(of(void 0));
    memberApi.mergeFavorites.and.returnValue(of({added: 3, skipped: 0, total: 3}));

    TestBed.configureTestingModule({
      providers: [
        {provide: PLATFORM_ID, useValue: 'browser'},
        {provide: CatalogApiService, useValue: api},
        {provide: CatalogMemberApiService, useValue: memberApi},
        {provide: CatalogAuthService, useValue: auth},
      ],
    });
    local = TestBed.inject(LocalFavoritesStore);
  });

  it('stores an anonymous favorite and declares it for the public count', async () => {
    const service = TestBed.inject(CatalogFavoritesService);

    const added = await service.add({
      isbn13: '9782070612758',
      title: 'Le Petit Prince',
      authors: 'Antoine de Saint-Exupéry',
      coverUrl: null,
    });

    expect(added).toBeTrue();
    expect(service.mode()).toBe('local');
    expect(local.list().map(favorite => favorite.isbn13)).toEqual(['9782070612758']);
    expect(api.declareAnonymousFavorite).toHaveBeenCalledOnceWith('9782070612758');
  });

  it('merges three local favorites once after sign-in, then clears the local list', async () => {
    const favorites = [
      favorite('9782070612758'),
      favorite('9782070408504'),
      favorite('9783140464079'),
    ];
    favorites.forEach(entry => local.add(entry));
    auth.isAuthenticated.set(true);
    const service = TestBed.inject(CatalogFavoritesService);

    await service.onSignedIn();

    expect(memberApi.mergeFavorites).toHaveBeenCalledOnceWith(
      'member-token',
      favorites.map(({isbn13, addedAt}) => ({isbn13, addedAt})),
    );
    expect(memberApi.getFavorites).toHaveBeenCalled();
    expect(local.list()).toEqual([]);
    expect(service.mode()).toBe('remote');
    expect(service.syncNotice()).toEqual({added: 3, skipped: 0, total: 3});
  });

  it('keeps the local heart threshold when it transfers favorites to the account', async () => {
    local.setMinimumHearts(4);
    local.add(favorite('9782070612758'));
    auth.isAuthenticated.set(true);
    const service = TestBed.inject(CatalogFavoritesService);

    await service.onSignedIn();

    expect(memberApi.setRecommendationPreference)
      .toHaveBeenCalledOnceWith('member-token', true, 4);
    expect(service.minimumHearts()).toBe(4);
  });

  it('preserves local favorites when the account merge fails', async () => {
    const entry = favorite('9782070612758');
    local.add(entry);
    auth.isAuthenticated.set(true);
    memberApi.mergeFavorites.and.returnValue(throwError(() => new Error('offline')));
    const service = TestBed.inject(CatalogFavoritesService);

    await expectAsync(service.onSignedIn()).toBeRejected();

    expect(local.list()).toEqual([entry]);
    expect(service.error()).toContain('La liste de ce navigateur est conservée');
  });

  it('undoes a favorite removal during the six-second window without deleting it remotely', async () => {
    const entry = favorite('9782070612758');
    local.add(entry);
    const service = TestBed.inject(CatalogFavoritesService);

    await service.remove(entry.isbn13);
    expect(service.favorites().map(item => item.isbn13)).toEqual([]);

    service.undoRemoval();

    expect(service.favorites().map(item => item.isbn13)).toEqual([entry.isbn13]);
    expect(local.list()).toEqual([entry]);
    expect(api.withdrawAnonymousFavorite).not.toHaveBeenCalled();
  });

  it('withdraws the anonymous declaration only after the six-second undo window', async () => {
    jasmine.clock().install();
    try {
    const entry = favorite('9782070612758');
    local.add(entry);
    const service = TestBed.inject(CatalogFavoritesService);

    await service.remove(entry.isbn13);
    jasmine.clock().tick(5999);
    expect(api.withdrawAnonymousFavorite).not.toHaveBeenCalled();

    jasmine.clock().tick(1);
    expect(api.withdrawAnonymousFavorite).toHaveBeenCalledOnceWith(entry.isbn13);
    expect(service.undoRemoval()).toBeFalse();
    } finally {
      jasmine.clock().uninstall();
    }
  });

  it('removes a member favorite only after the six-second undo window', async () => {
    jasmine.clock().install();
    try {
      auth.isAuthenticated.set(true);
      memberApi.getFavorites.and.returnValue(of(favoritesResponse('Ready')));
      const service = TestBed.inject(CatalogFavoritesService);
      await service.refresh();

      await service.remove('9782070612758');
      jasmine.clock().tick(5999);
      expect(memberApi.removeFavorite).not.toHaveBeenCalled();

      jasmine.clock().tick(1);
      await flushPromises();
      expect(memberApi.removeFavorite).toHaveBeenCalledOnceWith('member-token', '9782070612758');
    } finally {
      jasmine.clock().uninstall();
    }
  });

  it('refreshes computing recommendations every five seconds and stops when ready', async () => {
    jasmine.clock().install();
    try {
    const computing = favoritesResponse('Computing');
    const ready = favoritesResponse('Ready');
    memberApi.getFavorites.and.returnValues(of(computing), of(ready));
    auth.isAuthenticated.set(true);
    const service = TestBed.inject(CatalogFavoritesService);

    await service.refresh();
    expect(memberApi.getFavorites).toHaveBeenCalledTimes(1);
    expect(service.computingByIsbn()['9782070612758']).toBeTrue();

    jasmine.clock().tick(5000);
    await flushPromises();
    expect(memberApi.getFavorites).toHaveBeenCalledTimes(2);
    expect(service.computingByIsbn()['9782070612758']).toBeFalse();

    jasmine.clock().tick(5000);
    await flushPromises();
    expect(memberApi.getFavorites).toHaveBeenCalledTimes(2);
    } finally {
      jasmine.clock().uninstall();
    }
  });

  function favorite(isbn13: string) {
    return {
      isbn13,
      title: `Livre ${isbn13}`,
      authors: 'Auteur',
      coverUrl: null,
      addedAt: '2026-11-14T10:00:00.000Z',
    };
  }
});

function emptyFavorites(): CatalogMemberFavoritesResponse {
  return {generatedAt: '2026-11-14T10:00:00Z', total: 0, maximum: 200, works: []};
}

async function flushPromises(): Promise<void> {
  for (let index = 0; index < 8; index++) {
    await Promise.resolve();
  }
}

function favoritesResponse(status: string): CatalogMemberFavoritesResponse {
  return {
    generatedAt: '2026-11-14T10:00:00Z',
    total: 1,
    maximum: 200,
    works: [{
      workKey: 'work:123',
      workId: '123',
      title: 'Le Petit Prince',
      authors: 'Antoine de Saint-Exupéry',
      addedAt: '2026-11-14T10:00:00Z',
      editionCount: 1,
      isComputingSuggestions: status === 'Computing',
      editions: [{
        isbn13: '9782070612758',
        title: 'Le Petit Prince',
        authors: 'Antoine de Saint-Exupéry',
        coverUrl: null,
        addedAt: '2026-11-14T10:00:00Z',
        quantityAvailable: 1,
        quantityAnnounced: 0,
        favoriteCount: 1,
        recommendationStatus: status,
      }],
    }],
  };
}
