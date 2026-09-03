import {TestBed} from '@angular/core/testing';
import {MsalBroadcastService, MsalService} from '@azure/msal-angular';
import {AccountInfo} from '@azure/msal-browser';
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

  function createService(): AuthSessionService {
    return TestBed.inject(AuthSessionService);
  }

  beforeEach(() => {
    instance = jasmine.createSpyObj('PublicClientApplication', [
      'getActiveAccount',
      'getAllAccounts',
      'setActiveAccount',
      'clearCache',
    ]);
    instance.getActiveAccount.and.returnValue(null);
    instance.getAllAccounts.and.returnValue([]);

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
          useValue: {msalSubject$: new Subject(), inProgress$: new Subject()},
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

  it('falls back on the mailbox name when the account carries no display name', () => {
    instance.getAllAccounts.and.returnValue([accountFixture({name: undefined})]);

    expect(createService().displayName()).toBe('marie.dupont');
  });
});
