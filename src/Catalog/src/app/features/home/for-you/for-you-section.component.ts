import {ChangeDetectionStrategy, Component, OnInit, Signal, computed, signal} from '@angular/core';
import type {AccountInfo} from '@azure/msal-browser';

import {CatalogAuthService} from '../../../core/catalog-auth.service';
import {CatalogFavoriteSuggestion} from '../../../core/catalog.models';
import {CatalogFavoritesService} from '../../../core/favorites/catalog-favorites.service';
import {LocalFavoritesStore} from '../../../core/favorites/local-favorites.store';

type ForYouState = 'loading' | 'ready' | 'entry' | 'hidden';

@Component({
  selector: 'app-for-you-section',
  standalone: false,
  templateUrl: './for-you-section.component.html',
  styleUrls: ['./for-you-section.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ForYouSectionComponent implements OnInit {
  readonly state = signal<ForYouState>('loading');
  readonly items: Signal<CatalogFavoriteSuggestion[]> = computed(() => {
    const minimum = this.favorites.currentMinimumHearts();
    return this.favorites.suggestions()
      .filter(item => (item.hearts ?? 0) >= minimum)
      .slice(0, 4);
  });
  readonly skeletons = [0, 1, 2, 3];
  readonly firstName = computed(() => firstNameFromAccount(this.auth.account()));

  constructor(
    private readonly auth: CatalogAuthService,
    readonly favorites: CatalogFavoritesService,
    private readonly local: LocalFavoritesStore,
  ) {}

  ngOnInit(): void {
    void this.loadRecommendations();
  }

  dismissEntry(): void {
    this.local.setHomeEntryDismissed(true);
    this.state.set('hidden');
  }

  recommendationText(item: CatalogFavoriteSuggestion): string {
    return item.reason === 'NextTome'
      ? `La suite de ${item.seedTitle}`
      : `Parce que vous avez aimé ${item.seedTitle}`;
  }

  private async loadRecommendations(): Promise<void> {
    try {
      await this.auth.initialize();
      if (this.auth.isAuthenticated()) {
        await this.favorites.onSignedIn();
      } else {
        await this.favorites.refresh();
      }

      if (this.favorites.suggestionStatus() === 'Disabled') {
        this.state.set('hidden');
        return;
      }

      if (this.items().length >= 3) {
        this.state.set('ready');
      } else {
        this.state.set(this.local.homeEntryDismissed() ? 'hidden' : 'entry');
      }
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
