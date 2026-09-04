import {HttpRequest, HttpResponse} from '@angular/common/http';
import {MsalBroadcastService, MsalInterceptor, MsalService} from '@azure/msal-angular';
import {AccountInfo, AuthenticationResult, Logger, PublicClientApplication} from '@azure/msal-browser';
import {of} from 'rxjs';

import {environment} from '../../environments/environment';
import {msalInterceptorConfig} from './msal-config';

describe('MSAL scan interceptor configuration', () => {
  it('adds the API bearer token to nested scan requests', () => {
    const account = createAccount();
    const msal = createMsalService(account);
    const interceptor = new MsalInterceptor(
      msalInterceptorConfig,
      msal,
      {normalize: (url: string) => url} as never,
      {} as MsalBroadcastService,
      document,
    );
    const request = new HttpRequest('GET', `${environment.apiUrl}/scan/catalog/delta`);
    const next = {
      handle: jasmine.createSpy('handle').and.returnValue(of(new HttpResponse({status: 200}))),
    };

    interceptor.intercept(request, next).subscribe();

    expect(msal.acquireTokenSilent).toHaveBeenCalledWith({
      account,
      scopes: [environment.entra.apiScope],
    });
    const forwardedRequest = next.handle.calls.mostRecent().args[0] as HttpRequest<unknown>;
    expect(forwardedRequest.headers.get('Authorization')).toBe('Bearer api-access-token');
  });

  function createMsalService(account: AccountInfo): jasmine.SpyObj<MsalService> & {
    instance: PublicClientApplication;
  } {
    const logger = {
      warning: jasmine.createSpy('warning'),
      verbose: jasmine.createSpy('verbose'),
      info: jasmine.createSpy('info'),
      infoPii: jasmine.createSpy('infoPii'),
    };
    const instance = {
      getActiveAccount: jasmine.createSpy('getActiveAccount').and.returnValue(account),
      getAllAccounts: jasmine.createSpy('getAllAccounts').and.returnValue([account]),
    } as unknown as PublicClientApplication;
    const service = jasmine.createSpyObj<MsalService>(
      'MsalService',
      ['acquireTokenSilent'],
      {instance, getLogger: () => logger as unknown as Logger},
    ) as jasmine.SpyObj<MsalService> & {instance: PublicClientApplication};
    service.acquireTokenSilent.and.returnValue(of({
      accessToken: 'api-access-token',
    } as AuthenticationResult));
    return service;
  }

  function createAccount(): AccountInfo {
    return {
      homeAccountId: 'tri-home',
      environment: 'volepapillondamour.ciamlogin.com',
      tenantId: 'b23c80b3-9776-4840-8255-fcbf3b3500fd',
      username: 'tri@example.org',
      localAccountId: 'tri-local',
      name: 'Tri',
    };
  }
});
