import {Injectable} from '@angular/core';
import {MsalService} from '@azure/msal-angular';
import {AccountInfo} from '@azure/msal-browser';
import {Observable, throwError} from 'rxjs';
import {map} from 'rxjs/operators';

import {environment} from '../../../environments/environment';

@Injectable({
  providedIn: 'root',
})
export class ApiAccessTokenService {
  constructor(private readonly msalService: MsalService) {}

  public getApiAccessToken$(): Observable<string> {
    const account = this.getActiveAccount();

    if (!account) {
      return throwError(() => new Error('No active Entra account is available.'));
    }

    return this.msalService.acquireTokenSilent({
      account,
      scopes: [environment.entra.apiScope],
    }).pipe(map(result => result.accessToken));
  }

  private getActiveAccount(): AccountInfo | null {
    const activeAccount = this.msalService.instance.getActiveAccount();
    if (activeAccount) {
      return activeAccount;
    }

    const firstAccount = this.msalService.instance.getAllAccounts()[0] ?? null;
    if (firstAccount) {
      this.msalService.instance.setActiveAccount(firstAccount);
    }

    return firstAccount;
  }
}
