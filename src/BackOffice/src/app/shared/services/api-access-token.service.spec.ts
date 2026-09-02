import {TestBed} from '@angular/core/testing';
import {MsalService} from '@azure/msal-angular';
import {AccountInfo, AuthenticationResult} from '@azure/msal-browser';
import {firstValueFrom, of} from 'rxjs';

import {ApiAccessTokenService} from './api-access-token.service';

describe('ApiAccessTokenService', () => {
  const account = {
    homeAccountId: 'home-account-id',
    environment: 'volepapillondamour.ciamlogin.com',
    tenantId: 'tenant-id',
    username: 'volunteer@example.test',
    localAccountId: 'local-account-id',
    name: 'Volunteer',
  } as AccountInfo;

  let service: ApiAccessTokenService;
  let msalService: jasmine.SpyObj<MsalService>;

  beforeEach(() => {
    const instance = {
      getActiveAccount: jasmine.createSpy('getActiveAccount').and.returnValue(account),
      getAllAccounts: jasmine.createSpy('getAllAccounts').and.returnValue([account]),
      setActiveAccount: jasmine.createSpy('setActiveAccount'),
    };

    msalService = jasmine.createSpyObj<MsalService>('MsalService', ['acquireTokenSilent']);
    Object.defineProperty(msalService, 'instance', {value: instance});
    msalService.acquireTokenSilent.and.returnValue(of({accessToken: 'api-access-token'} as AuthenticationResult));

    TestBed.configureTestingModule({
      providers: [
        ApiAccessTokenService,
        {provide: MsalService, useValue: msalService},
      ],
    });

    service = TestBed.inject(ApiAccessTokenService);
  });

  it('acquires the API token for the active Entra account', async () => {
    const token = await firstValueFrom(service.getApiAccessToken$());

    expect(token).toBe('api-access-token');
    expect(msalService.acquireTokenSilent).toHaveBeenCalledWith({
      account,
      scopes: ['api://ebc68507-2c07-4bab-9448-2d6d489c6112/access_as_user'],
    });
  });

  it('selects and activates the first cached account when no active account exists', async () => {
    const instance = msalService.instance as unknown as {
      getActiveAccount: jasmine.Spy;
      getAllAccounts: jasmine.Spy;
      setActiveAccount: jasmine.Spy;
    };
    instance.getActiveAccount.and.returnValue(null);

    await firstValueFrom(service.getApiAccessToken$());

    expect(instance.setActiveAccount).toHaveBeenCalledWith(account);
  });

  it('fails when no Entra account is available', async () => {
    const instance = msalService.instance as unknown as {
      getActiveAccount: jasmine.Spy;
      getAllAccounts: jasmine.Spy;
    };
    instance.getActiveAccount.and.returnValue(null);
    instance.getAllAccounts.and.returnValue([]);

    await expectAsync(firstValueFrom(service.getApiAccessToken$()))
      .toBeRejectedWithError('No active Entra account is available.');
  });
});
