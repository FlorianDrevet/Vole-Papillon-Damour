import {ChangeDetectionStrategy, Component, OnInit, Signal, computed, signal} from '@angular/core';

import {CatalogAuthService} from '../../core/catalog-auth.service';
import {CatalogFavoriteMergeResult} from '../../core/catalog.models';
import {CatalogFavoritesService} from '../../core/favorites/catalog-favorites.service';
import {LocalFavoriteEntry, LocalFavoritesStore} from '../../core/favorites/local-favorites.store';

@Component({
  selector: 'app-catalog-favorites-page',
  standalone: false,
  templateUrl: './catalog-favorites-page.component.html',
  styleUrls: ['./catalog-favorites-page.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CatalogFavoritesPageComponent implements OnInit {
  readonly authReady = signal(false);
  readonly addPanelOpen = signal(false);
  readonly accountStepOpen = signal(false);
  readonly bannerCollapsed = signal(false);
  readonly removedTitle = signal<string | null>(null);
  readonly accountRecoveryNotice = signal<CatalogFavoriteMergeResult | null>(null);
  readonly registrationError = signal<string | null>(null);
  readonly isMember: Signal<boolean>;
  readonly visibleSuggestions = computed(() => this.favorites.suggestions().filter(
    suggestion => (suggestion.hearts ?? 0) >= this.favorites.currentMinimumHearts(),
  ));
  readonly favoriteCount = computed(() => this.favorites.favorites().length);
  readonly anyFavoriteComputing = computed(() => Object.values(this.favorites.computingByIsbn()).some(Boolean));
  readonly suggestionsDisabled = computed(() => this.favorites.suggestionStatus() === 'Disabled');

  constructor(
    readonly favorites: CatalogFavoritesService,
    readonly auth: CatalogAuthService,
    readonly local: LocalFavoritesStore,
  ) {
    this.isMember = auth.isAuthenticated;
    this.bannerCollapsed.set(local.bannerCollapsed());
  }

  ngOnInit(): void {
    void this.initializePage();
  }

  async setMinimumHearts(value: number): Promise<void> {
    await this.favorites.setMinimumHearts(value);
  }

  setBannerCollapsed(value: boolean): void {
    this.bannerCollapsed.set(value);
    this.local.setBannerCollapsed(value);
  }

  async remove(entry: LocalFavoriteEntry): Promise<void> {
    await this.favorites.remove(entry.isbn13);
    this.removedTitle.set(entry.title);
    window.setTimeout(() => this.removedTitle.set(null), 6000);
  }

  undoRemoval(): void {
    if (this.favorites.undoRemoval()) {
      this.removedTitle.set(null);
    }
  }

  openAddPanel(): void {
    this.addPanelOpen.set(true);
  }

  closeAddPanel(): void {
    this.addPanelOpen.set(false);
  }

  openAccountStep(): void {
    this.accountStepOpen.set(true);
  }

  closeAccountStep(): void {
    this.accountStepOpen.set(false);
  }

  async createAccount(fairDigestConsent: boolean): Promise<void> {
    this.registrationError.set(null);
    try {
      await this.auth.registerFromFavorites('/trouver-mon-prochain-livre', fairDigestConsent);
    } catch {
      this.registrationError.set('La création de votre compte n’a pas pu démarrer. Réessayez.');
    }
  }

  private async initializePage(): Promise<void> {
    try {
      await this.auth.initialize();
      const registrationReturn = this.auth.consumeFavoriteRegistrationReturn();
      if (this.auth.isAuthenticated()) {
        await this.favorites.onSignedIn();
        if (registrationReturn) {
          if (registrationReturn.fairDigestConsent) {
            await this.favorites.setFairDigestConsent(true);
          }
          this.accountRecoveryNotice.set(this.favorites.syncNotice() ?? {
            added: 0,
            skipped: 0,
            total: this.favorites.favorites().length,
          });
        }
      } else {
        await this.favorites.refresh();
      }
    } catch {
      // The favorites service owns the inline error state and preserves local entries.
    } finally {
      this.authReady.set(true);
    }
  }
}
