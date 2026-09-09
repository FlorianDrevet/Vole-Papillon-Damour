import {MsalBroadcastService, MsalService} from '@azure/msal-angular';
import {
  AccountInfo,
  AuthenticationResult,
  EventMessage,
  EventType,
  InteractionStatus,
  PublicClientApplication,
} from '@azure/msal-browser';
import {BehaviorSubject, of, Subject, throwError} from 'rxjs';

import {loginRequest} from './msal-config';
import {ScanAuthService, ScanAuthState} from './scan-auth.service';

describe('ScanAuthService', () => {
  it('waits for MSAL interactions to finish before reading the cached account', () => {
    const account = createAccount('benevole@example.org', 'Bénévole', ['Tri']);
    const instance = createMsalInstance([account]);
    const msal = createMsalService(instance, createAccessToken(['Tri']));
    const broadcast = createBroadcastService(InteractionStatus.Startup);

    new ScanAuthService(msal, broadcast.service);

    expect(instance.getActiveAccount).not.toHaveBeenCalled();
    expect(instance.getAllAccounts).not.toHaveBeenCalled();

    broadcast.progress.next(InteractionStatus.None);

    expect(instance.getActiveAccount).toHaveBeenCalled();
    expect(instance.getAllAccounts).toHaveBeenCalled();
  });

  it('publishes the cached account and uses the scan API scope for login', () => {
    const account = createAccount('benevole@example.org', 'Bénévole', ['Tri']);
    const accounts = [account];
    const instance = createMsalInstance(accounts);
    const msal = createMsalService(instance, createAccessToken(['Tri']));
    const broadcast = createBroadcastService();

    const service = new ScanAuthService(msal, broadcast.service);

    expect(service.isAuthenticated).toBeTrue();
    expect(service.isAuthorized).toBeTrue();
    expect(service.canSort).toBeTrue();
    expect(service.canSell).toBeFalse();
    expect(service.authState.status).toBe('authorized');
    expect(service.displayName).toBe('Bénévole');
    expect(instance.setActiveAccount).toHaveBeenCalledOnceWith(account);

    service.login().subscribe();
    service.logout();

    expect(msal.loginRedirect).toHaveBeenCalledOnceWith({
      ...loginRequest,
      redirectStartPage: `${window.location.origin}/`,
    });
    expect(msal.logoutRedirect).toHaveBeenCalledOnceWith();
  });

  it('updates the account when MSAL broadcasts login and logout', () => {
    const initialAccount = createAccount('initial@example.org', 'Initial', ['Tri']);
    const loggedInAccount = createAccount('tri@example.org', 'Tri', ['Tri']);
    const accounts = [initialAccount];
    const instance = createMsalInstance(accounts);
    const msal = createMsalService(instance, createAccessToken(['Tri']));
    const broadcast = createBroadcastService();
    const service = new ScanAuthService(msal, broadcast.service);

    accounts.splice(0, 1, loggedInAccount);
    broadcast.subject.next({
      eventType: EventType.LOGIN_SUCCESS,
      payload: {account: loggedInAccount} as AuthenticationResult,
    } as EventMessage);

    expect(service.displayName).toBe('Tri');
    expect(service.isAuthenticated).toBeTrue();

    accounts.splice(0, 1);
    instance.getActiveAccount.and.returnValue(null);
    broadcast.subject.next({eventType: EventType.LOGOUT_SUCCESS} as EventMessage);

    expect(service.isAuthenticated).toBeFalse();
    expect(service.displayName).toBeNull();
  });

  it('authorizes a caisse account for sales without granting sorting access', () => {
    const account = createAccount('caisse@example.org', 'Caisse', ['Caisse']);
    const instance = createMsalInstance([account]);
    const service = new ScanAuthService(
      createMsalService(instance, createAccessToken(['Caisse'])),
      createBroadcastService().service,
    );

    expect(service.isAuthenticated).toBeTrue();
    expect(service.isAuthorized).toBeTrue();
    expect(service.canSort).toBeFalse();
    expect(service.canSell).toBeTrue();
    expect(service.authState.status).toBe('authorized');
    expect(service.roles).toEqual(['Caisse']);
  });

  it('uses API access-token roles instead of cached ID-token roles', () => {
    const account = createAccount('administrator@example.org', 'Administrateur', ['Administration']);
    const instance = createMsalInstance([account]);
    const msal = createMsalService(instance, createAccessToken(['Administration', 'Tri']));

    const service = new ScanAuthService(
      msal,
      createBroadcastService().service,
    );

    expect(msal.acquireTokenSilent).toHaveBeenCalledWith({
      account,
      scopes: loginRequest.scopes,
    });
    expect(service.isAuthorized).toBeTrue();
    expect(service.canSort).toBeTrue();
    expect(service.roles).toEqual(['Administration', 'Tri']);
  });

  it('keeps the cached account in degraded mode when silent token renewal fails', () => {
    const account = createAccount('tri@example.org', 'Tri', ['Tri']);
    const instance = createMsalInstance([account]);
    const broadcast = createBroadcastService();
    const service = new ScanAuthService(
      createMsalService(instance, createAccessToken(['Tri'])),
      broadcast.service,
    );

    broadcast.subject.next({eventType: EventType.ACQUIRE_TOKEN_FAILURE} as EventMessage);

    expect(service.isAuthenticated).toBeTrue();
    expect(service.isAuthorized).toBeFalse();
    expect(service.authState.status).toBe('degraded');
    expect(service.canSort).toBeTrue();
  });

  it('restores the local authorization marker after a fresh service instance', () => {
    const storageKey = 'vpd-scan-local-authorization';
    localStorage.removeItem(storageKey);
    const account = createAccount('cached-tri@example.org', 'Tri', ['Tri']);
    const firstInstance = createMsalInstance([account]);
    new ScanAuthService(
      createMsalService(firstInstance, createAccessToken(['Tri'])),
      createBroadcastService().service,
    );

    const secondInstance = createMsalInstance([account]);
    const secondMsal = createMsalService(secondInstance, createAccessToken(['Tri']));
    secondMsal.acquireTokenSilent.and.returnValue(throwError(() => new Error('offline')));
    const rehydrated = new ScanAuthService(secondMsal, createBroadcastService().service);

    expect(rehydrated.authState.status).toBe('degraded');
    expect(rehydrated.canSort).toBeTrue();

    rehydrated.handleServerAuthorizationFailure();

    expect(rehydrated.authState.status).toBe('unauthenticated');
    expect(localStorage.getItem(storageKey)).toBeNull();
  });

  function createBroadcastService(initialStatus: InteractionStatus = InteractionStatus.None): {
    service: MsalBroadcastService;
    subject: Subject<EventMessage>;
    progress: BehaviorSubject<InteractionStatus>;
  } {
    const subject = new Subject<EventMessage>();
    const progress = new BehaviorSubject<InteractionStatus>(initialStatus);
    return {
      service: {
        msalSubject$: subject.asObservable(),
        inProgress$: progress.asObservable(),
      } as MsalBroadcastService,
      subject,
      progress,
    };
  }

  function createMsalService(
    instance: PublicClientApplication,
    accessToken: string,
  ): jasmine.SpyObj<MsalService> & {
    instance: PublicClientApplication;
  } {
    const service = jasmine.createSpyObj<MsalService>(
      'MsalService',
      ['loginRedirect', 'logoutRedirect', 'acquireTokenSilent'],
      {instance},
    ) as jasmine.SpyObj<MsalService> & {instance: PublicClientApplication};
    service.loginRedirect.and.returnValue(of(undefined));
    service.logoutRedirect.and.returnValue(of(undefined));
    service.acquireTokenSilent.and.returnValue(of({accessToken} as AuthenticationResult));
    return service;
  }

  function createAccessToken(roles: string[]): string {
    const encode = (value: object) =>
      btoa(JSON.stringify(value))
        .replace(/\+/g, '-')
        .replace(/\//g, '_')
        .replace(/=+$/g, '');
    return `${encode({alg: 'none', typ: 'JWT'})}.${encode({roles})}.signature`;
  }

  function createMsalInstance(accounts: AccountInfo[]): PublicClientApplication & {
    getActiveAccount: jasmine.Spy;
    getAllAccounts: jasmine.Spy;
    setActiveAccount: jasmine.Spy;
  } {
    const instance = {
      getActiveAccount: jasmine.createSpy('getActiveAccount').and.returnValue(null),
      getAllAccounts: jasmine.createSpy('getAllAccounts').and.callFake(() => accounts),
      setActiveAccount: jasmine.createSpy('setActiveAccount'),
    };
    return instance as unknown as PublicClientApplication & {
      getActiveAccount: jasmine.Spy;
      getAllAccounts: jasmine.Spy;
      setActiveAccount: jasmine.Spy;
    };
  }

  function createAccount(username: string, name: string, roles: string[] = []): AccountInfo {
    return {
      homeAccountId: `${username}-home`,
      environment: 'volepapillondamour.ciamlogin.com',
      tenantId: 'b23c80b3-9776-4840-8255-fcbf3b3500fd',
      username,
      localAccountId: `${username}-local`,
      name,
      idTokenClaims: {roles},
    };
  }
});
