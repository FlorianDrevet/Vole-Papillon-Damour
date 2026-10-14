import {Injectable} from '@angular/core';
import {firstValueFrom} from 'rxjs';

import {ScanManualRequestCandidate} from './scan-offline.model';
import {ScanApiService} from './scan-api.service';

export type ScanManualRequestLookup =
  | {state: 'none'}
  | {state: 'loading'}
  | {state: 'found'; candidate: ScanManualRequestCandidate}
  | {state: 'unavailable'};

@Injectable({providedIn: 'root'})
export class ScanManualRequestService {
  constructor(private readonly api: ScanApiService) {}

  async lookup(isbn13: string, online: boolean): Promise<ScanManualRequestLookup> {
    if (!online) {
      return {state: 'unavailable'};
    }

    try {
      const candidate = await firstValueFrom(this.api.getManualRequestCandidate(isbn13));
      return candidate ? {state: 'found', candidate} : {state: 'none'};
    } catch {
      return {state: 'unavailable'};
    }
  }
}
