import {Injectable} from '@angular/core';
import {MsalService} from '@azure/msal-angular';
import {AccountInfo, InteractionRequiredAuthError} from '@azure/msal-browser';
import {catchError, Observable, throwError} from 'rxjs';
import {map} from 'rxjs/operators';

import {environment} from '../../../environments/environment';
import {loginRequest} from '../auth/msal-config';

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
    }).pipe(
      map(result => result.accessToken),
      catchError((error: unknown) => {
        // Le compte est encore en cache mais Entra refuse de renouveler le jeton
        // sans intervention (session expirée, consentement révoqué, MFA demandé).
        // Renvoyer sur l'écran de connexion ne débloquerait rien — le compte y est
        // toujours vu comme valide : seule une redirection interactive rétablit la
        // session, on la déclenche donc directement.
        if (error instanceof InteractionRequiredAuthError) {
          this.msalService
            .acquireTokenRedirect({...loginRequest, account})
            .subscribe({error: () => undefined});
        }

        return throwError(() => error);
      }),
    );
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
