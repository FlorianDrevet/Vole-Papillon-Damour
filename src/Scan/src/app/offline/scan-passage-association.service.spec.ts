import {HttpErrorResponse} from '@angular/common/http';
import {of, throwError} from 'rxjs';

import {ScanApiService} from './scan-api.service';
import {ScanLocalStoreService} from './scan-local-store.service';
import {ScanPassageAssociationService} from './scan-passage-association.service';

describe('ScanPassageAssociationService', () => {
  let api: jasmine.SpyObj<ScanApiService>;
  let store: jasmine.SpyObj<ScanLocalStoreService>;
  let service: ScanPassageAssociationService;

  beforeEach(() => {
    api = jasmine.createSpyObj<ScanApiService>('ScanApiService', ['resolveMemberCard']);
    store = jasmine.createSpyObj<ScanLocalStoreService>(
      'ScanLocalStoreService',
      ['putPassageAssociation', 'listPassageAssociations'],
    );
    store.putPassageAssociation.and.resolveTo();
    store.listPassageAssociations.and.resolveTo([]);
    service = new ScanPassageAssociationService(api, store);
  });

  it('resolves online and exposes only the display label', async () => {
    api.resolveMemberCard.and.returnValue(of({displayLabel: 'Camille'}));
    service.startAssociation();

    await service.submitCredential('VPDC1.AAAA.BBBB');

    expect(service.state()).toEqual({kind: 'associated', displayLabel: 'Camille'});
    expect(JSON.stringify(service.state())).not.toContain('VPDC1.AAAA.BBBB');
  });

  it('falls back to pending-offline on network error', async () => {
    api.resolveMemberCard.and.returnValue(throwError(() => new HttpErrorResponse({status: 0})));

    await service.submitCredential('VPDC1.AAAA.BBBB');

    expect(service.state().kind).toBe('pending-offline');
  });

  it('marks not-recognised on 404 and keeps the sale anonymous on commit', async () => {
    api.resolveMemberCard.and.returnValue(throwError(() => new HttpErrorResponse({status: 404})));

    await service.submitCredential('VPDC1.AAAA.BBBB');

    expect(service.state().kind).toBe('not-recognised');
    expect(await service.commit('p1', new Date())).toBeFalse();
    expect(store.putPassageAssociation).not.toHaveBeenCalled();
    expect(await store.listPassageAssociations()).toEqual([]);
  });

  it('persists only passage id, credential and dates on commit', async () => {
    api.resolveMemberCard.and.returnValue(of({displayLabel: 'Camille'}));
    await service.submitCredential('lune 4271');

    await service.commit('p1', new Date('2026-03-14T15:00:00Z'));

    const entry = store.putPassageAssociation.calls.mostRecent().args[0];
    expect(Object.keys(entry).sort()).toEqual([
      'attemptCount',
      'checkoutPassageId',
      'createdAt',
      'credential',
      'lastAttemptAt',
      'lastError',
      'occurredAt',
      'status',
    ]);
    expect(entry.credential).toEqual({kind: 'recovery-code', value: 'LUNE-4271'});
    expect(entry.checkoutPassageId).toBe('p1');
    expect(entry.occurredAt).toBe('2026-03-14T15:00:00.000Z');
    expect(JSON.stringify(entry)).not.toContain('Camille');
  });
});
