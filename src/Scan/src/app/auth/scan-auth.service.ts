import {Injectable} from '@angular/core';
import {MsalBroadcastService, MsalService} from '@azure/msal-angular';
import {AccountInfo, AuthenticationResult, EventType} from '@azure/msal-browser';
import {BehaviorSubject} from 'rxjs';
import {filter} from 'rxjs/operators';

import {loginRequest} from './msal-config';

@Injectable({providedIn: 'root'})
export class ScanAuthService {
  private readonly accountSubject = new BehaviorSubject<AccountInfo | null>(null);
  readonly account$ = this.accountSubject.asObservable();

  constructor(
    private readonly msalService: MsalService,
    private readonly msalBroadcastService: MsalBroadcastService,
  ) {
    this.publishCachedAccount();

    this.msalBroadcastService.msalSubject$
      .pipe(filter(message =>
        message.eventType === EventType.LOGIN_SUCCESS ||
        message.eventType === EventType.LOGOUT_SUCCESS))
      .subscribe(message => {
        if (message.eventType === EventType.LOGIN_SUCCESS) {
          const result = message.payload as AuthenticationResult;
          if (result.account) {
            this.msalService.instance.setActiveAccount(result.account);
          }
        }

        this.publishCachedAccount();
      });
  }

  get isAuthenticated(): boolean {
    return this.accountSubject.value !== null;
  }

  get displayName(): string | null {
    const account = this.accountSubject.value;
    return account?.name || account?.username || null;
  }

  login(): void {
    this.msalService.loginRedirect(loginRequest).subscribe({
      error: () => this.publishCachedAccount(),
    });
  }

  logout(): void {
    this.msalService.logoutRedirect().subscribe({
      error: () => this.publishCachedAccount(),
    });
  }

  private publishCachedAccount(): void {
    const activeAccount = this.msalService.instance.getActiveAccount();
    if (activeAccount) {
      this.accountSubject.next(activeAccount);
      return;
    }

    const firstAccount = this.msalService.instance.getAllAccounts()[0] ?? null;
    if (firstAccount) {
      this.msalService.instance.setActiveAccount(firstAccount);
    }
    this.accountSubject.next(firstAccount);
  }
}
