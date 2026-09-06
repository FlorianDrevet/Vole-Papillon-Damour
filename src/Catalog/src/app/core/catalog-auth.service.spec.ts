import type {AccountInfo, IPublicClientApplication} from '@azure/msal-browser';
import {TestBed} from '@angular/core/testing';

import {environment} from '../../environments/environment';
import {
  CATALOG_MSAL_LOADER,
  CatalogAuthenticationRedirectStartedError,
  CatalogMsalModule,
  CatalogAuthService,
} from './catalog-auth.service';

describe('CatalogAuthService', () => {
  const account = {
    homeAccountId: 'home-account-id',
    environment: 'volepapillondamour.ciamlogin.com',
    tenantId: environment.entra.tenantId,
    username: 'administrator@example.test',
    localAccountId: 'local-account-id',
    name: 'Administrator',
  } as AccountInfo;

  let service: CatalogAuthService;
  let client: jasmine.SpyObj<IPublicClientApplication>;
  let loader: jasmine.Spy;
  let msalModule: CatalogMsalModule;

  beforeEach(() => {
    client = jasmine.createSpyObj<IPublicClientApplication>('PublicClientApplication', [
      'initialize',
      'handleRedirectPromise',
      'getActiveAccount',
      'getAllAccounts',
      'setActiveAccount',
      'loginRedirect',
      'logoutRedirect',
      'acquireTokenSilent',
      'acquireTokenRedirect',
    ]);
    client.initialize.and.resolveTo();
    client.handleRedirectPromise.and.resolveTo(null);
    client.getActiveAccount.and.returnValue(account);
    client.getAllAccounts.and.returnValue([account]);
    client.loginRedirect.and.resolveTo();
    client.logoutRedirect.and.resolveTo();
    client.acquireTokenSilent.and.resolveTo({accessToken: 'api-access-token'} as never);
    client.acquireTokenRedirect.and.resolveTo();

    const PublicClientApplication = jasmine.createSpy('PublicClientApplication')
      .and.returnValue(client);
    msalModule = {
      PublicClientApplication,
      BrowserCacheLocation: {LocalStorage: 'localStorage'},
      InteractionRequiredAuthError: class extends Error {},
    } as unknown as CatalogMsalModule;
    loader = jasmine.createSpy('loadMsal').and.resolveTo(msalModule);

    TestBed.configureTestingModule({
      providers: [
        CatalogAuthService,
        {provide: CATALOG_MSAL_LOADER, useValue: loader},
      ],
    });
    service = TestBed.inject(CatalogAuthService);
  });

  it('initializes MSAL once and selects the returned account', async () => {
    await service.initialize();
    await service.initialize();

    expect(loader).toHaveBeenCalledTimes(1);
    expect(client.initialize).toHaveBeenCalledTimes(1);
    expect(service.account()).toBe(account);
    expect(service.isAuthenticated()).toBeTrue();
  });

  it('returns to the administration page after starting an interactive login', async () => {
    await service.login('/administration');

    expect(client.loginRedirect).toHaveBeenCalledWith({
      scopes: [environment.entra.apiScope],
      redirectStartPage: new URL('/administration', window.location.origin).href,
    });
  });

  it('starts account registration with the create prompt and returns to the account page', async () => {
    await service.register('/compte');

    expect(client.loginRedirect).toHaveBeenCalledWith({
      scopes: [environment.entra.apiScope],
      prompt: 'create',
      redirectStartPage: new URL('/compte', window.location.origin).href,
    });
  });

  it('acquires an API token for the active account', async () => {
    const token = await service.getApiAccessToken();

    expect(token).toBe('api-access-token');
    expect(client.acquireTokenSilent).toHaveBeenCalledWith({
      account,
      scopes: [environment.entra.apiScope],
    });
  });

  it('recognizes the administration role from the API access token', async () => {
    client.acquireTokenSilent.and.resolveTo({
      accessToken: createAccessToken(['Administration']),
    } as never);

    await service.getApiAccessToken();

    expect(service.isAdministrator()).toBeTrue();
    expect(service.roles()).toEqual(['Administration']);
  });

  it('keeps the legacy Admin role compatible with the API policy', async () => {
    client.acquireTokenSilent.and.resolveTo({
      accessToken: createAccessToken(['Admin']),
    } as never);

    await service.getApiAccessToken();

    expect(service.isAdministrator()).toBeTrue();
  });

  it('does not infer administration access from the cached ID token', async () => {
    const accountWithIdTokenRole = {
      ...account,
      idTokenClaims: {roles: ['Administration']},
    } as AccountInfo;
    client.getActiveAccount.and.returnValue(accountWithIdTokenRole);
    client.getAllAccounts.and.returnValue([accountWithIdTokenRole]);

    await service.getApiAccessToken();

    expect(service.isAdministrator()).toBeFalse();
  });

  it('fails clearly when no cached account is available', async () => {
    client.getActiveAccount.and.returnValue(null);
    client.getAllAccounts.and.returnValue([]);

    await expectAsync(service.getApiAccessToken())
      .toBeRejectedWithError('No active Entra account is available.');
  });

  it('starts an interactive token request when silent acquisition needs interaction', async () => {
    await service.initialize();
    const interactionRequiredError = new msalModule.InteractionRequiredAuthError(
      'interaction_required',
      'correlation-id',
    );
    client.acquireTokenSilent.and.rejectWith(interactionRequiredError);

    await expectAsync(service.getApiAccessToken())
      .toBeRejectedWithError(CatalogAuthenticationRedirectStartedError);

    expect(client.acquireTokenRedirect).toHaveBeenCalledWith({
      account,
      scopes: [environment.entra.apiScope],
      redirectStartPage: window.location.href,
    });
  });

  function createAccessToken(roles: string[]): string {
    const encode = (value: object) =>
      btoa(JSON.stringify(value))
        .replace(/\+/g, '-')
        .replace(/\//g, '_')
        .replace(/=+$/g, '');
    return `${encode({alg: 'none', typ: 'JWT'})}.${encode({roles})}.signature`;
  }
});
