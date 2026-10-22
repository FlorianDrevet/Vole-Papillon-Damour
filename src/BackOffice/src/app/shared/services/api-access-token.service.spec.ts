import {TestBed} from '@angular/core/testing';
import {MsalBroadcastService, MsalService} from '@azure/msal-angular';
import {AccountInfo, AuthenticationResult, InteractionStatus} from '@azure/msal-browser';
import {BehaviorSubject, firstValueFrom, of, throwError} from 'rxjs';

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
  let inProgress$: BehaviorSubject<InteractionStatus>;

  beforeEach(() => {
    inProgress$ = new BehaviorSubject<InteractionStatus>(InteractionStatus.None);

    const instance = {
      getActiveAccount: jasmine.createSpy('getActiveAccount').and.returnValue(account),
      getAllAccounts: jasmine.createSpy('getAllAccounts').and.returnValue([account]),
      setActiveAccount: jasmine.createSpy('setActiveAccount'),
    };

    msalService = jasmine.createSpyObj<MsalService>('MsalService', [
      'acquireTokenSilent',
      'acquireTokenRedirect',
    ]);
    Object.defineProperty(msalService, 'instance', {value: instance});
    msalService.acquireTokenSilent.and.returnValue(of({accessToken: 'api-access-token'} as AuthenticationResult));
    msalService.acquireTokenRedirect.and.returnValue(of(undefined));

    TestBed.configureTestingModule({
      providers: [
        ApiAccessTokenService,
        {provide: MsalService, useValue: msalService},
        {provide: MsalBroadcastService, useValue: {inProgress$}},
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

  it('forces a silent token refresh when the API rejects the cached token', async () => {
    const token = await firstValueFrom(service.getApiAccessToken$(true));

    expect(token).toBe('api-access-token');
    expect(msalService.acquireTokenSilent).toHaveBeenCalledWith({
      account,
      scopes: ['api://ebc68507-2c07-4bab-9448-2d6d489c6112/access_as_user'],
      forceRefresh: true,
    });
  });

  it('waits for MSAL to finish its interaction before acquiring the API token', async () => {
    const instance = msalService.instance as unknown as {
      getActiveAccount: jasmine.Spy;
    };
    inProgress$.next(InteractionStatus.Startup);

    const tokenPromise = firstValueFrom(service.getApiAccessToken$());

    expect(instance.getActiveAccount).not.toHaveBeenCalled();
    expect(msalService.acquireTokenSilent).not.toHaveBeenCalled();

    inProgress$.next(InteractionStatus.None);
    const token = await tokenPromise;

    expect(token).toBe('api-access-token');
    expect(instance.getActiveAccount).toHaveBeenCalled();
    expect(msalService.acquireTokenSilent).toHaveBeenCalled();
  });

  it('selects and activates the sole cached account when no active account exists', async () => {
    const instance = msalService.instance as unknown as {
      getActiveAccount: jasmine.Spy;
      getAllAccounts: jasmine.Spy;
      setActiveAccount: jasmine.Spy;
    };
    instance.getActiveAccount.and.returnValue(null);

    await firstValueFrom(service.getApiAccessToken$());

    expect(instance.setActiveAccount).toHaveBeenCalledWith(account);
  });

  it('requires explicit account selection when multiple cached accounts have no active account', async () => {
    const instance = msalService.instance as unknown as {
      getActiveAccount: jasmine.Spy;
      getAllAccounts: jasmine.Spy;
      setActiveAccount: jasmine.Spy;
    };
    instance.getActiveAccount.and.returnValue(null);
    instance.getAllAccounts.and.returnValue([
      account,
      {...account, homeAccountId: 'second-account-id', username: 'second@example.test'} as AccountInfo,
    ]);

    await expectAsync(firstValueFrom(service.getApiAccessToken$()))
      .toBeRejectedWithError('No active Entra account is available.');

    expect(instance.setActiveAccount).not.toHaveBeenCalled();
    expect(msalService.acquireTokenSilent).not.toHaveBeenCalled();
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

  it('starts interactive renewal when the silent iframe times out before the API request', async () => {
    const silentError = {errorCode: 'monitor_window_timeout'};
    msalService.acquireTokenSilent.and.returnValue(throwError(() => silentError));

    let rejectedError: unknown;
    try {
      await firstValueFrom(service.getApiAccessToken$());
    } catch (error) {
      rejectedError = error;
    }

    expect(rejectedError).toBe(silentError);
    expect(msalService.acquireTokenRedirect).toHaveBeenCalledWith({
      account,
      scopes: ['api://ebc68507-2c07-4bab-9448-2d6d489c6112/access_as_user'],
      redirectStartPage: window.location.href,
    });
  });
});
