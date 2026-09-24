import {signal, WritableSignal} from '@angular/core';
import {HttpErrorResponse} from '@angular/common/http';
import {TestBed} from '@angular/core/testing';
import {of, throwError} from 'rxjs';

import {CatalogAuthService} from '../catalog-auth.service';
import {CatalogMemberApiService} from '../catalog-member-api.service';
import {CatalogSelectionResponse} from '../catalog.models';
import {LocalSelectionStore} from './local-selection.store';
import {CatalogSelectionService} from './catalog-selection.service';

describe('CatalogSelectionService', () => {
  let api: jasmine.SpyObj<CatalogMemberApiService>;
  let auth: {isAuthenticated: WritableSignal<boolean>; getApiAccessToken: jasmine.Spy};
  let local: LocalSelectionStore;

  beforeEach(() => {
    localStorage.removeItem('vpd.catalog.selection.v1');
    api = jasmine.createSpyObj('CatalogMemberApiService', [
      'getSelection', 'addSelectionItem', 'removeSelectionItem', 'mergeSelection'
    ]);
    auth = {
      isAuthenticated: signal(false),
      getApiAccessToken: jasmine.createSpy().and.resolveTo('token')
    };
    TestBed.configureTestingModule({providers: [
      {provide: CatalogMemberApiService, useValue: api},
      {provide: CatalogAuthService, useValue: auth},
    ]});
    local = TestBed.inject(LocalSelectionStore);
  });

  it('stores locally and never calls the API when anonymous', async () => {
    const service = TestBed.inject(CatalogSelectionService);

    await service.add({kind: 'edition', isbn13: '9782070612758'}, 'Le Petit Prince');

    expect(service.keys().has('edition:9782070612758')).toBeTrue();
    expect(api.addSelectionItem).not.toHaveBeenCalled();
  });

  it('asks for confirmation when both lists have entries, then merges and clears local storage', async () => {
    local.add({ref: {kind: 'edition', isbn13: '1'}, title: 'a', addedAt: '2026-09-20T10:00:00.000Z'});
    local.add({ref: {kind: 'edition', isbn13: '2'}, title: 'b', addedAt: '2026-09-20T10:00:00.000Z'});
    api.getSelection.and.returnValue(of(selectionResponse(['3'])));
    api.mergeSelection.and.returnValue(of({added: 2, alreadyPresent: 0, rejected: []}));
    const service = TestBed.inject(CatalogSelectionService);
    auth.isAuthenticated.set(true);

    await service.onSignedIn();
    expect(service.pendingMerge()).toEqual({localCount: 2, accountCount: 1, mergedCount: 3});
    expect(api.mergeSelection).not.toHaveBeenCalled();

    api.getSelection.and.returnValue(of(selectionResponse(['1', '2', '3'])));
    await service.confirmMerge();

    expect(local.list()).toEqual([]);
    expect(service.keys().size).toBe(3);
    expect(service.mode()).toBe('synced');
  });

  it('keeps rejected entries locally after merge', async () => {
    local.add({ref: {kind: 'edition', isbn13: '1'}, title: 'a', addedAt: '2026-09-20T10:00:00.000Z'});
    api.getSelection.and.returnValue(of(selectionResponse([])));
    api.mergeSelection.and.returnValue(of({added: 0, alreadyPresent: 0, rejected: ['1']}));
    const service = TestBed.inject(CatalogSelectionService);
    auth.isAuthenticated.set(true);

    await service.onSignedIn();

    expect(local.list().length).toBe(1);
  });

  it('keeps local entries and marks them unsynced when the merge is declined', async () => {
    local.add({ref: {kind: 'edition', isbn13: '1'}, title: 'a', addedAt: '2026-09-20T10:00:00.000Z'});
    api.getSelection.and.returnValue(of(selectionResponse(['3'])));
    const service = TestBed.inject(CatalogSelectionService);
    auth.isAuthenticated.set(true);

    await service.onSignedIn();
    service.declineMerge();

    expect(service.mode()).toBe('local-unsynced');
    expect(local.list().length).toBe(1);
    expect(api.mergeSelection).not.toHaveBeenCalled();
  });

  it('keeps local storage when the merge call fails', async () => {
    local.add({ref: {kind: 'edition', isbn13: '1'}, title: 'a', addedAt: '2026-09-20T10:00:00.000Z'});
    api.getSelection.and.returnValue(of(selectionResponse([])));
    api.mergeSelection.and.returnValue(throwError(() => new HttpErrorResponse({status: 503})));
    const service = TestBed.inject(CatalogSelectionService);
    auth.isAuthenticated.set(true);

    await expectAsync(service.onSignedIn()).toBeRejected();

    expect(local.list().length).toBe(1);
  });

  it('does not add locally when a signed-in API add fails with 401', async () => {
    api.getSelection.and.returnValue(of(selectionResponse([])));
    const service = TestBed.inject(CatalogSelectionService);
    auth.isAuthenticated.set(true);
    await service.onSignedIn();
    api.addSelectionItem.and.returnValue(throwError(() => new HttpErrorResponse({status: 401})));

    await expectAsync(service.add({kind: 'edition', isbn13: '1'}, 'a')).toBeRejected();

    expect(local.list()).toEqual([]);
  });});

function selectionResponse(isbns: string[]): CatalogSelectionResponse {
  return {
    generatedAt: '2026-09-23T10:00:00.000Z',
    nextFair: null,
    items: isbns.map((isbn13, index) => ({
      id: `item-${index}`,
      kind: 'edition',
      isbn13,
      rareBookId: null,
      rareBookSlug: null,
      title: `Book ${isbn13}`,
      authors: null,
      publisher: null,
      publicationYear: null,
      physicalFormat: null,
      coverUrl: null,
      availability: 'Available',
      availabilityCheckedAt: '2026-09-23T10:00:00.000Z',
      status: 'ToTake',
      notFoundReport: null,
      addedAt: '2026-09-23T10:00:00.000Z',
      purchasedAt: null
    }))
  };
}
