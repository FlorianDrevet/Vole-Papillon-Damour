import {TestBed} from '@angular/core/testing';
import {PLATFORM_ID} from '@angular/core';
import {LocalSelectionStore} from './local-selection.store';
import {selectionKey} from './selection-merge';

describe('LocalSelectionStore', () => {
  const key = 'vpd.catalog.selection.v1';
  const entry = (isbn13: string) => ({
    ref: {kind: 'edition' as const, isbn13},
    title: 'Le Petit Prince',
    addedAt: '2026-09-23T10:00:00.000Z'
  });

  beforeEach(() => localStorage.removeItem(key));

  function create(platformId = 'browser'): LocalSelectionStore {
    TestBed.configureTestingModule({providers: [{provide: PLATFORM_ID, useValue: platformId}]});
    return TestBed.inject(LocalSelectionStore);
  }

  it('survives a reload', () => {
    create().add(entry('9782070612758'));
    TestBed.resetTestingModule();

    expect(create().list().map(e => selectionKey(e.ref))).toEqual(['edition:9782070612758']);
  });

  it('does not duplicate the same edition', () => {
    const store = create();
    store.add(entry('9782070612758'));
    store.add(entry('9782070612758'));

    expect(store.list().length).toBe(1);
  });

  it('returns an empty list when storage is corrupted and keeps the raw value', () => {
    localStorage.setItem(key, '{not json');

    expect(create().list()).toEqual([]);
    expect(localStorage.getItem(key)).toBe('{not json');
  });

  it('is inert on the server', () => {
    const store = create('server');
    store.add(entry('9782070612758'));

    expect(store.available).toBeFalse();
    expect(store.list()).toEqual([]);
    expect(localStorage.getItem(key)).toBeNull();
  });

  it('is inert when localStorage throws', () => {
    spyOn(Storage.prototype, 'getItem').and.throwError('SecurityError');

    const store = create();

    expect(store.available).toBeFalse();
    expect(store.list()).toEqual([]);
  });

  it('reports an existing rare book selection and removes only the matching reference', () => {
    const store = create();
    const edition = entry('9782070612758');
    const rareBook = {
      ref: {kind: 'rare' as const, rareBookId: 'e56118db-233a-4bb2-931d-4d2c50d98917'},
      title: 'Édition rare',
      addedAt: '2026-09-23T10:00:00.000Z'
    };
    store.add(edition);
    store.add(rareBook);

    expect(store.has(rareBook.ref)).toBeTrue();
    store.remove(edition.ref);

    expect(store.list().map(item => selectionKey(item.ref))).toEqual([
      'rare:e56118db-233a-4bb2-931d-4d2c50d98917'
    ]);
  });

  it('clears the anonymous selection', () => {
    const store = create();
    store.add(entry('9782070612758'));

    store.clear();

    expect(store.list()).toEqual([]);
    expect(localStorage.getItem(key)).toBeNull();
  });
});
