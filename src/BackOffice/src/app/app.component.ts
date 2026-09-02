import {Component} from '@angular/core';
import {MsalBroadcastService, MsalService} from '@azure/msal-angular';
import {AuthenticationResult, EventType} from '@azure/msal-browser';
import {filter} from 'rxjs/operators';
import {takeUntilDestroyed} from '@angular/core/rxjs-interop';

@Component({
    selector: 'app-root',
    templateUrl: './app.component.html',
    styleUrl: './app.component.scss',
    standalone: false
})
export class AppComponent {
  title = 'Template Angular'; //TODO Change title

  constructor(
    private readonly msalService: MsalService,
    private readonly msalBroadcastService: MsalBroadcastService,
  ) {
    this.setActiveAccountFromCache();

    this.msalBroadcastService.msalSubject$
      .pipe(
        filter(message => message.eventType === EventType.LOGIN_SUCCESS),
        takeUntilDestroyed(),
      )
      .subscribe(message => {
        const result = message.payload as AuthenticationResult;
        this.msalService.instance.setActiveAccount(result.account);
      });
  }

  private setActiveAccountFromCache(): void {
    const activeAccount = this.msalService.instance.getActiveAccount();
    if (activeAccount) {
      return;
    }

    const firstAccount = this.msalService.instance.getAllAccounts()[0];
    if (firstAccount) {
      this.msalService.instance.setActiveAccount(firstAccount);
    }
  }
}
