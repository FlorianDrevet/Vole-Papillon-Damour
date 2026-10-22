import {TestBed} from '@angular/core/testing';
import {MsalBroadcastService, MsalService} from '@azure/msal-angular';
import {AccountInfo, AuthenticationResult, EventMessage, EventType} from '@azure/msal-browser';
import {of, Subject} from 'rxjs';

import {AuthSessionService} from './auth-session.service';

function accountFixture(overrides: Partial<AccountInfo> = {}): AccountInfo {
  return {
    homeAccountId: 'home-id',
    environment: 'login.microsoftonline.com',
    tenantId: 'tenant-id',
    localAccountId: 'local-id',
    username: 'marie.dupont@volepapillondamour.fr',
    name: 'Marie Dupont',
    ...overrides,
  } as AccountInfo;
}

describe('AuthSessionService', () => {
  let instance: jasmine.SpyObj<any>;
  let msalService: jasmine.SpyObj<MsalService>;
  let msalEvents: Subject<EventMessage>;
  let activeAccount: AccountInfo | null;

  function createService(): AuthSessionService {
    return TestBed.inject(AuthSessionService);
  }

  beforeEach(() => {
    activeAccount = null;
    msalEvents = new Subject<EventMessage>();
    instance = jasmine.createSpyObj('PublicClientApplication', [
      'getActiveAccount',
      'getAllAccounts',
      'setActiveAccount',
      'clearCache',
    ]);
    instance.getActiveAccount.and.callFake(() => activeAccount);
    instance.getAllAccounts.and.returnValue([]);
    instance.setActiveAccount.and.callFake((account: AccountInfo | null) => {
      activeAccount = account;
    });

    msalService = jasmine.createSpyObj<MsalService>(
      'MsalService',
      ['loginRedirect', 'logoutRedirect'],
      {instance} as Partial<MsalService>,
    );
    msalService.loginRedirect.and.returnValue(of(undefined));
    msalService.logoutRedirect.and.returnValue(of(undefined));

    TestBed.configureTestingModule({
      providers: [
        {provide: MsalService, useValue: msalService},
        {
          provide: MsalBroadcastService,
          useValue: {msalSubject$: msalEvents, inProgress$: new Subject()},
        },
      ],
    });
  });

  it('sends the user into the application after a redirect login, not back to the login screen', () => {
    createService().login();

    const request = msalService.loginRedirect.calls.mostRecent().args[0] as any;
    expect(request.scopes).toEqual(['api://ebc68507-2c07-4bab-9448-2d6d489c6112/access_as_user']);
    expect(request.redirectStartPage).toBe(`${window.location.origin}/actualites`);
  });

  it('adopts the cached account as the active one', () => {
    const account = accountFixture();
    instance.getAllAccounts.and.returnValue([account]);

    const service = createService();

    expect(instance.setActiveAccount).toHaveBeenCalledWith(account);
    expect(service.isAuthenticated()).toBeTrue();
    expect(service.displayName()).toBe('Marie Dupont');
    expect(service.initials()).toBe('MD');
  });

  it('does not choose the first account when multiple accounts are cached', () => {
    const firstAccount = accountFixture();
    const secondAccount = accountFixture({
      homeAccountId: 'second-home-id',
      username: 'other@example.org',
      name: 'Autre bénévole',
    });
    instance.getAllAccounts.and.returnValue([firstAccount, secondAccount]);

    const service = createService();

    expect(instance.setActiveAccount).not.toHaveBeenCalled();
    expect(service.isAuthenticated()).toBeFalse();
    expect(service.account()).toBeNull();
  });

  it('asks Entra to select an account when more than one is cached', () => {
    instance.getAllAccounts.and.returnValue([
      accountFixture(),
      accountFixture({homeAccountId: 'second-home-id', username: 'other@example.org'}),
    ]);

    createService().login();

    const request = msalService.loginRedirect.calls.mostRecent().args[0] as any;
    expect(request.prompt).toBe('select_account');
  });

  it('activates the exact account returned by a successful login', () => {
    const firstAccount = accountFixture();
    const selectedAccount = accountFixture({
      homeAccountId: 'selected-home-id',
      username: 'selected@example.org',
      name: 'Compte choisi',
    });
    instance.getAllAccounts.and.returnValue([firstAccount, selectedAccount]);
    const service = createService();

    msalEvents.next({
      eventType: EventType.LOGIN_SUCCESS,
      payload: {account: selectedAccount} as AuthenticationResult,
    } as EventMessage);

    expect(instance.setActiveAccount).toHaveBeenCalledWith(selectedAccount);
    expect(service.account()).toBe(selectedAccount);
  });

  it('falls back on the mailbox name when the account carries no display name', () => {
    instance.getAllAccounts.and.returnValue([accountFixture({name: undefined})]);

    expect(createService().displayName()).toBe('marie.dupont');
  });
});
