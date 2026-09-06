import {ChangeDetectionStrategy, Component, OnInit, Signal, signal} from '@angular/core';
import {HttpErrorResponse} from '@angular/common/http';
import {Meta} from '@angular/platform-browser';
import {firstValueFrom} from 'rxjs';

import {CatalogAuthService} from '../../core/catalog-auth.service';
import {CatalogMemberApiService} from '../../core/catalog-member-api.service';

@Component({
  selector: 'app-catalog-unsubscribe-page',
  standalone: false,
  templateUrl: './catalog-unsubscribe-page.component.html',
  styleUrls: ['./catalog-unsubscribe-page.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CatalogUnsubscribePageComponent implements OnInit {
  readonly initialized: Signal<boolean>;
  readonly isAuthenticated: Signal<boolean>;
  readonly authError: Signal<string | null>;
  readonly pending = signal(false);
  readonly completed = signal(false);
  readonly errorMessage = signal<string | null>(null);

  constructor(
    private readonly auth: CatalogAuthService,
    private readonly api: CatalogMemberApiService,
    private readonly meta: Meta,
  ) {
    this.initialized = this.auth.initialized;
    this.isAuthenticated = this.auth.isAuthenticated;
    this.authError = this.auth.error;
  }

  ngOnInit(): void {
    this.meta.updateTag({name: 'robots', content: 'noindex, nofollow'});
    void this.initialize();
  }

  async initialize(): Promise<void> {
    await this.auth.initialize();
    if (!this.auth.isAuthenticated()) {
      try {
        await this.auth.login('/desinscription');
      } catch {
        this.errorMessage.set('La connexion n’a pas pu être démarrée. Ouvrez votre compte pour vous désabonner.');
      }
    }
  }

  async confirmUnsubscribe(): Promise<void> {
    if (!this.auth.isAuthenticated() || this.pending() || this.completed()) {
      return;
    }

    this.pending.set(true);
    this.errorMessage.set(null);
    try {
      const token = await this.auth.getApiAccessToken();
      await firstValueFrom(this.api.setAlertStatus(token, false));
      this.completed.set(true);
    } catch (error: unknown) {
      this.errorMessage.set(error instanceof HttpErrorResponse && error.status === 401
        ? 'La session a expiré. Reconnectez-vous puis réessayez.'
        : 'Le désabonnement n’a pas pu être enregistré. Réessayez dans un instant.');
    } finally {
      this.pending.set(false);
    }
  }
}
