import {TestBed} from '@angular/core/testing';
import {of, throwError} from 'rxjs';

import {ScanApiService} from './scan-api.service';
import {ScanManualRequestService} from './scan-manual-request.service';

describe('ScanManualRequestService', () => {
  let service: ScanManualRequestService;
  let api: jasmine.SpyObj<ScanApiService>;

  beforeEach(() => {
    api = jasmine.createSpyObj<ScanApiService>('ScanApiService', ['getManualRequestCandidate']);
    TestBed.configureTestingModule({
      providers: [
        ScanManualRequestService,
        {provide: ScanApiService, useValue: api},
      ],
    });
    service = TestBed.inject(ScanManualRequestService);
  });

  it('returns the matching requester for a successful lookup', async () => {
    const candidate = {
      requestId: 'request-1',
      firstName: 'Camille',
      lastName: 'DUPONT',
      phone: '0612345678',
    };
    api.getManualRequestCandidate.and.returnValue(of(candidate));

    expect(await service.lookup('9782070363735', true)).toEqual({state: 'found', candidate});
  });

  it('returns none when the API has no matching requester', async () => {
    api.getManualRequestCandidate.and.returnValue(of(null));

    expect(await service.lookup('9782070363735', true)).toEqual({state: 'none'});
  });

  it('does not call the API while offline', async () => {
    expect(await service.lookup('9782070363735', false)).toEqual({state: 'unavailable'});
    expect(api.getManualRequestCandidate).not.toHaveBeenCalled();
  });

  it('treats network and authorization errors as unavailable', async () => {
    api.getManualRequestCandidate.and.returnValue(throwError(() => new Error('offline')));

    expect(await service.lookup('9782070363735', true)).toEqual({state: 'unavailable'});
  });
});
