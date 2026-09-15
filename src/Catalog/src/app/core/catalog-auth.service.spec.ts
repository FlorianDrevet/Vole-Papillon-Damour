import type {AccountInfo, IPublicClientApplication} from '@azure/msal-browser';
import {TestBed} from '@angular/core/testing';

import {environment} from '../../environments/environment';
import {
  CATALOG_MSAL_LOADER,
  CATALOG_SILENT_SESSION_TIMEOUT_MS,
  CatalogAuthenticationRedirectStartedError,
  CatalogMsalModule,
  CatalogAuthService,
} from './catalog-auth.service';
import {catalogLoginRequest, catalogRegistrationRequest} from './catalog-auth.config';

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
    sessionStorage.clear();
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
    expect(service.state()).toBe('authenticated');
    expect(service.isAuthenticated()).toBeTrue();
  });

  it('signs a cached account out on load when its session needs interaction', async () => {
    const interactionRequiredError = new msalModule.InteractionRequiredAuthError(
      'interaction_required',
      'correlation-id',
    );
    client.acquireTokenSilent.and.rejectWith(interactionRequiredError);

    await service.initialize();

    expect(service.state()).toBe('signed-out');
    expect(service.requiresReauthentication()).toBeFalse();
    expect(service.isAuthenticated()).toBeFalse();
    expect(service.account()).toBeNull();
    expect(service.recognizedAccount()).toBe(account);
  });

  it('does not keep an unverified cached session signed in after a non-interactive failure', async () => {
    client.acquireTokenSilent.and.rejectWith({errorCode: 'monitor_window_timeout'} as never);

    await service.initialize();

    expect(service.isAuthenticated()).toBeFalse();
    expect(service.account()).toBeNull();
    expect(service.recognizedAccount()).toBe(account);
  });

  it('stops waiting for a silent session restore after the timeout', async () => {
    client.acquireTokenSilent.and.returnValue(new Promise(() => undefined));
    jasmine.clock().install();
    try {
      const initialization = service.initialize();
      for (let i = 0; i < 50 && !client.acquireTokenSilent.calls.any(); i++) {
        await Promise.resolve();
      }
      jasmine.clock().tick(CATALOG_SILENT_SESSION_TIMEOUT_MS);
      await initialization;
    } finally {
      jasmine.clock().uninstall();
    }

    expect(service.initialized()).toBeTrue();
    expect(service.isAuthenticated()).toBeFalse();
    expect(service.recognizedAccount()).toBe(account);
  });

  it('sends a recognized account to the login page once per browser session', async () => {
    client.acquireTokenSilent.and.rejectWith({errorCode: 'login_required'} as never);
    await service.initialize();

    expect(await service.resumeRecognizedSession('/compte')).toBeTrue();
    expect(await service.resumeRecognizedSession('/compte')).toBeFalse();

    expect(client.loginRedirect).toHaveBeenCalledOnceWith({
      ...catalogLoginRequest,
      loginHint: account.username,
      redirectStartPage: new URL('/compte', window.location.origin).href,
    });
  });

  it('does not resume a session for an anonymous visitor', async () => {
    client.getActiveAccount.and.returnValue(null);
    client.getAllAccounts.and.returnValue([]);

    expect(await service.resumeRecognizedSession('/compte')).toBeFalse();
    expect(client.loginRedirect).not.toHaveBeenCalled();
  });

  it('restores separate name claims from a cached account token', async () => {
    const cachedAccount = {...account, name: 'unknown'} as AccountInfo;
    const idTokenClaims = {
      given_name: 'Camille',
      family_name: 'Dupont',
    };
    client.getActiveAccount.and.returnValue(cachedAccount);
    client.getAllAccounts.and.returnValue([cachedAccount]);
    client.acquireTokenSilent.and.resolveTo({
      accessToken: 'api-access-token',
      idTokenClaims,
    } as never);

    await service.initialize();

    expect(service.account()?.idTokenClaims).toEqual(idTokenClaims);
  });

  it('keeps separate name claims returned by the interactive login', async () => {
    const idTokenClaims = {
      given_name: 'Camille',
      family_name: 'Dupont',
    };
    client.handleRedirectPromise.and.resolveTo({
      account: {...account, name: 'unknown'},
      accessToken: 'api-access-token',
      idTokenClaims,
    } as never);

    await service.initialize();

    expect(service.account()?.idTokenClaims).toEqual(idTokenClaims);
  });

  it('returns to the administration page after starting an interactive login', async () => {
    await service.login('/administration');

    expect(client.loginRedirect).toHaveBeenCalledWith({
      ...catalogLoginRequest,
      redirectStartPage: new URL('/administration', window.location.origin).href,
    });
  });

  it('starts account registration with the create prompt and returns to the account page', async () => {
    await service.register('/compte');

    expect(client.loginRedirect).toHaveBeenCalledWith({
      ...catalogRegistrationRequest,
      redirectStartPage: new URL('/compte', window.location.origin).href,
    });
  });

  it('acquires an API token for the active account', async () => {
    const token = await service.getApiAccessToken();

    expect(token).toBe('api-access-token');
    expect(service.state()).toBe('authenticated');
    expect(client.acquireTokenSilent).toHaveBeenCalledWith({
      account,
      scopes: [environment.entra.apiScope],
    });
  });

  it('force refreshes an API token after a protected request rejects the cached token', async () => {
    await service.initialize();
    client.acquireTokenSilent.calls.reset();

    const token = await service.refreshApiAccessToken();

    expect(token).toBe('api-access-token');
    expect(client.acquireTokenSilent).toHaveBeenCalledWith({
      account,
      scopes: [environment.entra.apiScope],
      forceRefresh: true,
    });
    expect(service.state()).toBe('authenticated');
  });

  it('does not start an interactive token request for passive token checks', async () => {
    await service.initialize();
    const interactionRequiredError = new msalModule.InteractionRequiredAuthError(
      'interaction_required',
      'correlation-id',
    );
    client.acquireTokenSilent.and.rejectWith(interactionRequiredError);

    const token = await service.tryGetApiAccessToken();

    expect(token).toBeNull();
    expect(client.acquireTokenRedirect).not.toHaveBeenCalled();
  });

  it('recognizes the administration role from the API access token', async () => {
    client.acquireTokenSilent.and.resolveTo({
      accessToken: createAccessToken(['Administration']),
    } as never);

    await service.getApiAccessToken();

    expect(service.isAdministrator()).toBeTrue();
    expect(service.roles()).toEqual(['Administration']);
  });

  it('recognizes tri and caisse roles as volunteer access', async () => {
    client.acquireTokenSilent.and.resolveTo({
      accessToken: createAccessToken(['Tri', 'Caisse']),
    } as never);

    await service.getApiAccessToken();

    expect(service.isVolunteer()).toBeTrue();
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
      ...catalogLoginRequest,
      account,
      redirectStartPage: window.location.href,
    });
  });

  it('recognizes an interaction-required error across an MSAL boundary', async () => {
    await service.initialize();
    client.acquireTokenSilent.and.rejectWith({
      errorCode: 'interaction_required',
      message: 'An interactive token request is required.',
    } as never);

    await expectAsync(service.getApiAccessToken())
      .toBeRejectedWithError(CatalogAuthenticationRedirectStartedError);

    expect(client.acquireTokenRedirect).toHaveBeenCalledWith({
      ...catalogLoginRequest,
      account,
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
