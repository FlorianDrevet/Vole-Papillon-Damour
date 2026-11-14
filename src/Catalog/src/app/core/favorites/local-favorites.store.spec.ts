import {TestBed} from '@angular/core/testing';
import {PLATFORM_ID} from '@angular/core';

import {LocalFavoritesStore} from './local-favorites.store';

describe('LocalFavoritesStore', () => {
  const key = 'vpd.catalog.favorites.v1';

  beforeEach(() => {
    localStorage.removeItem(key);
    localStorage.removeItem(`${key}.minimumHearts`);
    localStorage.removeItem(`${key}.bannerCollapsed`);
    localStorage.removeItem(`${key}.homeEntryDismissed`);
  });

  function create(platformId = 'browser'): LocalFavoritesStore {
    TestBed.configureTestingModule({providers: [{provide: PLATFORM_ID, useValue: platformId}]});
    return TestBed.inject(LocalFavoritesStore);
  }

  it('marks storage unavailable and stays safe when localStorage refuses writes', () => {
    spyOn(Storage.prototype, 'setItem').and.throwError('SecurityError');

    const store = create();

    expect(store.available).toBeFalse();
    expect(() => store.add(entry('9782070612758'))).not.toThrow();
    expect(store.list()).toEqual([]);
  });

  it('keeps at most fifty favorite entries', () => {
    const store = create();
    for (let index = 0; index < 50; index++) {
      expect(store.add(entry(index.toString().padStart(13, '0')))).toBeTrue();
    }

    expect(store.add(entry('9999999999999'))).toBeFalse();
    expect(store.list().length).toBe(50);
  });

  it('does not duplicate an ISBN and can remove it', () => {
    const store = create();
    const favorite = entry('9782070612758');

    expect(store.add(favorite)).toBeTrue();
    expect(store.add(favorite)).toBeFalse();
    store.remove(favorite.isbn13);

    expect(store.list()).toEqual([]);
  });

  it('lists the most recently added favorite first', () => {
    const store = create();
    store.add({...entry('9782070612758'), addedAt: '2026-11-13T10:00:00Z'});
    store.add({...entry('9782070408504'), addedAt: '2026-11-14T10:00:00Z'});

    expect(store.list().map(favorite => favorite.isbn13))
      .toEqual(['9782070408504', '9782070612758']);
  });

  it('keeps preference and dismissal settings when favorites are cleared', () => {
    const store = create();
    store.setMinimumHearts(4);
    store.setBannerCollapsed(true);
    store.setHomeEntryDismissed(true);
    store.add(entry('9782070612758'));

    store.clear();

    expect(store.list()).toEqual([]);
    expect(store.minimumHearts()).toBe(4);
    expect(store.bannerCollapsed()).toBeTrue();
    expect(store.homeEntryDismissed()).toBeTrue();
  });

  it('is inert on the server', () => {
    const store = create('server');

    expect(store.available).toBeFalse();
    expect(store.add(entry('9782070612758'))).toBeFalse();
    expect(store.list()).toEqual([]);
  });

  function entry(isbn13: string) {
    return {
      isbn13,
      title: 'Le Petit Prince',
      authors: 'Antoine de Saint-Exupéry',
      coverUrl: null,
      addedAt: '2026-11-14T10:00:00.000Z',
    };
  }
});
