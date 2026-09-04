import {MsalBroadcastService, MsalService} from '@azure/msal-angular';
import {
  AccountInfo,
  AuthenticationResult,
  EventMessage,
  EventType,
  PublicClientApplication,
} from '@azure/msal-browser';
import {of, Subject} from 'rxjs';

import {loginRequest} from './msal-config';
import {ScanAuthService, ScanAuthState} from './scan-auth.service';

describe('ScanAuthService', () => {
  it('publishes the cached account and uses the scan API scope for login', () => {
    const account = createAccount('benevole@example.org', 'Bénévole', ['Tri']);
    const accounts = [account];
    const instance = createMsalInstance(accounts);
    const msal = createMsalService(instance);
    const broadcast = createBroadcastService();

    const service = new ScanAuthService(msal, broadcast.service);

    expect(service.isAuthenticated).toBeTrue();
    expect(service.isAuthorized).toBeTrue();
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
    const msal = createMsalService(instance);
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

  it('denies access to an authenticated account without the Tri role', () => {
    const account = createAccount('caisse@example.org', 'Caisse', ['Caisse']);
    const instance = createMsalInstance([account]);
    const service = new ScanAuthService(
      createMsalService(instance),
      createBroadcastService().service,
    );

    expect(service.isAuthenticated).toBeTrue();
    expect(service.isAuthorized).toBeFalse();
    expect(service.authState.status).toBe('unauthorized');
    expect(service.roles).toEqual(['Caisse']);
  });

  it('returns to the login state when silent token renewal fails', () => {
    const account = createAccount('tri@example.org', 'Tri', ['Tri']);
    const instance = createMsalInstance([account]);
    const broadcast = createBroadcastService();
    const service = new ScanAuthService(createMsalService(instance), broadcast.service);

    broadcast.subject.next({eventType: EventType.ACQUIRE_TOKEN_FAILURE} as EventMessage);

    expect(service.isAuthenticated).toBeFalse();
    expect(service.isAuthorized).toBeFalse();
    expect(service.authState.status).toBe('unauthenticated');
  });

  function createBroadcastService(): {
    service: MsalBroadcastService;
    subject: Subject<EventMessage>;
  } {
    const subject = new Subject<EventMessage>();
    return {
      service: {msalSubject$: subject.asObservable()} as MsalBroadcastService,
      subject,
    };
  }

  function createMsalService(instance: PublicClientApplication): jasmine.SpyObj<MsalService> & {
    instance: PublicClientApplication;
  } {
    const service = jasmine.createSpyObj<MsalService>(
      'MsalService',
      ['loginRedirect', 'logoutRedirect'],
      {instance},
    ) as jasmine.SpyObj<MsalService> & {instance: PublicClientApplication};
    service.loginRedirect.and.returnValue(of(undefined));
    service.logoutRedirect.and.returnValue(of(undefined));
    return service;
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
