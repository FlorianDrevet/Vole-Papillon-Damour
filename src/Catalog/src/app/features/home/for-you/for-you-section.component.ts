import {ChangeDetectionStrategy, Component, OnInit, computed, signal} from '@angular/core';
import type {AccountInfo} from '@azure/msal-browser';
import {firstValueFrom} from 'rxjs';

import {CatalogAuthService} from '../../../core/catalog-auth.service';
import {CatalogMemberApiService} from '../../../core/catalog-member-api.service';
import {CatalogPersonalRecommendation} from '../../../core/catalog.models';

type ForYouState = 'loading' | 'ready' | 'hidden';

@Component({
  selector: 'app-for-you-section',
  standalone: false,
  templateUrl: './for-you-section.component.html',
  styleUrls: ['./for-you-section.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ForYouSectionComponent implements OnInit {
  readonly state = signal<ForYouState>('hidden');
  readonly items = signal<CatalogPersonalRecommendation[]>([]);
  readonly skeletons = [0, 1, 2, 3];
  readonly firstName = computed(() => firstNameFromAccount(this.auth.account()));

  constructor(
    private readonly auth: CatalogAuthService,
    private readonly memberApi: CatalogMemberApiService,
  ) {}

  ngOnInit(): void {
    void this.loadRecommendations();
  }

  private async loadRecommendations(): Promise<void> {
    try {
      await this.auth.initialize();
      if (!this.auth.isAuthenticated()) {
        return;
      }

      this.state.set('loading');
      const accessToken = await this.auth.tryGetApiAccessToken();
      if (!accessToken) {
        this.state.set('hidden');
        return;
      }

      const response = await firstValueFrom(this.memberApi.getRecommendations(accessToken, 4));
      if (response.status !== 'Enabled' || response.items.length < 3) {
        this.state.set('hidden');
        return;
      }

      this.items.set(response.items.slice(0, 4));
      this.state.set('ready');
    } catch {
      this.state.set('hidden');
    }
  }
}

function firstNameFromAccount(account: AccountInfo | null): string | null {
  if (!account) {
    return null;
  }

  const claims = account.idTokenClaims as Record<string, unknown> | undefined;
  const claimGivenName = claims?.['given_name'] ?? claims?.['givenName'];
  const givenName = typeof claimGivenName === 'string' ? claimGivenName.trim() : '';
  const displayName = givenName || account.name?.trim() || '';
  return displayName ? displayName.split(/\s+/)[0] : null;
}
