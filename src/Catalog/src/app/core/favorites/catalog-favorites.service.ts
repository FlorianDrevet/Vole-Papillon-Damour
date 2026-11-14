import {computed, Injectable, Signal, signal} from '@angular/core';
import {firstValueFrom} from 'rxjs';

import {CatalogAuthService} from '../catalog-auth.service';
import {CatalogApiService} from '../catalog-api.service';
import {CatalogMemberApiService} from '../catalog-member-api.service';
import {
  CatalogFavoriteAddedResponse,
  CatalogFavoriteMergeResult,
  CatalogFavoriteSuggestion,
  CatalogMemberFavoritesResponse,
} from '../catalog.models';
import {LocalFavoriteEntry, LocalFavoritesStore} from './local-favorites.store';

export type CatalogFavoriteMode = 'local' | 'remote';

interface PendingRemoval {
  entry: LocalFavoriteEntry;
  mode: CatalogFavoriteMode;
  timer: ReturnType<typeof setTimeout>;
  createdAt: number;
  undoable: boolean;
}

const UNDO_DURATION_MS = 6000;
const FAVORITE_REFRESH_INTERVAL_MS = 5000;
const MAX_POLL_DURATION_MS = 60_000;
const MAX_MEMBER_FAVORITES = 200;

@Injectable({providedIn: 'root'})
export class CatalogFavoritesService {
  private readonly localRevision = signal(0);
  private readonly remoteFavorites = signal<CatalogMemberFavoritesResponse | null>(null);
  private readonly pendingRemovals = signal<ReadonlyMap<string, PendingRemoval>>(new Map());
  private readonly suggestionItems = signal<CatalogFavoriteSuggestion[]>([]);
  private readonly currentComputing = signal<Readonly<Record<string, boolean>>>({});
  private readonly currentSuggestionStatus = signal<string>('Enabled');
  private readonly currentSyncNotice = signal<CatalogFavoriteMergeResult | null>(null);
  private readonly memberPreference = signal<{
    enabled: boolean;
    minimumHearts: number;
    fairDigestConsentedAt: string | null;
  } | null>(null);
  private readonly digestConsent = signal(false);
  private readonly currentError = signal<string | null>(null);
  private readonly refreshStartedAt = signal<number | null>(null);
  private refreshTimer: ReturnType<typeof setTimeout> | null = null;
  private refreshInFlight: Promise<void> | null = null;
  private signInInFlight: Promise<void> | null = null;

  readonly mode: Signal<CatalogFavoriteMode> = computed(() =>
    this.auth.isAuthenticated() ? 'remote' : 'local',
  );
  readonly favorites: Signal<LocalFavoriteEntry[]> = computed(() => {
    this.localRevision();
    const remote = this.mode() === 'remote' ? this.remoteEntries() : [];
    const local = this.local.list();
    const seen = new Set(remote.map(entry => entry.isbn13));
    const combined = [...remote, ...local.filter(entry => !seen.has(entry.isbn13))];
    const pending = this.pendingRemovals();
    return combined
      .filter(entry => !pending.has(entry.isbn13))
      .sort((left, right) => Date.parse(right.addedAt) - Date.parse(left.addedAt));
  });
  readonly favoriteIsbns: Signal<ReadonlySet<string>> = computed(() =>
    new Set(this.favorites().map(entry => entry.isbn13)),
  );
  readonly suggestions = this.suggestionItems.asReadonly();
  readonly computingByIsbn = this.currentComputing.asReadonly();
  readonly suggestionStatus = this.currentSuggestionStatus.asReadonly();
  readonly syncNotice = this.currentSyncNotice.asReadonly();
  readonly error = this.currentError.asReadonly();
  readonly fairDigestConsent = this.digestConsent.asReadonly();
  readonly currentMinimumHearts: Signal<number> = computed(() => {
    this.localRevision();
    return this.mode() === 'remote'
      ? this.memberPreference()?.minimumHearts ?? this.local.minimumHearts()
      : this.local.minimumHearts();
  });

  constructor(
    private readonly local: LocalFavoritesStore,
    private readonly api: CatalogApiService,
    private readonly memberApi: CatalogMemberApiService,
    private readonly auth: CatalogAuthService,
  ) {}

  minimumHearts(): number {
    return this.currentMinimumHearts();
  }

  async setMinimumHearts(value: number): Promise<void> {
    const normalized = Math.min(4, Math.max(1, Math.round(Number.isFinite(value) ? value : 3)));
    if (this.mode() === 'remote') {
      const token = await this.auth.getApiAccessToken();
      let preference = this.memberPreference();
      if (!preference) {
        const accountPreference = await firstValueFrom(
          this.memberApi.getRecommendationPreference(token),
        );
        preference = {
          enabled: accountPreference.enabled,
          minimumHearts: accountPreference.minimumHearts ?? 3,
          fairDigestConsentedAt: accountPreference.fairDigestConsentedAt ?? null,
        };
        this.digestConsent.set(Boolean(preference.fairDigestConsentedAt));
      }
      await firstValueFrom(this.memberApi.setRecommendationPreference(
        token,
        preference.enabled,
        normalized,
      ));
      this.memberPreference.set({...preference, minimumHearts: normalized});
    } else {
      this.local.setMinimumHearts(normalized);
      this.localRevision.update(value => value + 1);
    }
    await this.refresh().catch(() => undefined);
  }

  async setFairDigestConsent(consented: boolean): Promise<void> {
    if (this.mode() !== 'remote') {
      return;
    }

    const token = await this.auth.getApiAccessToken();
    let preference = this.memberPreference();
    if (!preference) {
      const accountPreference = await firstValueFrom(
        this.memberApi.getRecommendationPreference(token),
      );
      preference = {
        enabled: accountPreference.enabled,
        minimumHearts: accountPreference.minimumHearts ?? 3,
        fairDigestConsentedAt: accountPreference.fairDigestConsentedAt ?? null,
      };
    }

    await firstValueFrom(this.memberApi.setRecommendationPreference(
      token,
      preference.enabled,
      preference.minimumHearts,
      consented,
    ));
    const fairDigestConsentedAt = consented ? new Date().toISOString() : null;
    this.memberPreference.set({...preference, fairDigestConsentedAt});
    this.digestConsent.set(consented);
  }

  async add(entry: Omit<LocalFavoriteEntry, 'addedAt'>): Promise<boolean> {
    if (this.favoriteIsbns().has(entry.isbn13)) {
      return false;
    }

    if (this.mode() === 'local') {
      const locallyStoredCount = this.local.list().length + this.localPendingRemovalCount();
      if (!this.local.available || locallyStoredCount >= 50) {
        return false;
      }

      const favorite: LocalFavoriteEntry = {...entry, addedAt: new Date().toISOString()};
      if (!this.local.add(favorite)) {
        return false;
      }
      this.localRevision.update(value => value + 1);
      this.currentError.set(null);
      try {
        await firstValueFrom(this.api.declareAnonymousFavorite(entry.isbn13));
      } catch {
        // The browser list remains usable when the public counter is offline.
      }
      await this.refresh().catch(() => undefined);
      return true;
    }

    if (!this.remoteFavorites()) {
      await this.refresh();
    }
    const snapshot = this.remoteFavorites();
    if ((snapshot?.total ?? 0) >= (snapshot?.maximum ?? MAX_MEMBER_FAVORITES)) {
      return false;
    }

    const token = await this.auth.getApiAccessToken();
    const added: CatalogFavoriteAddedResponse = await firstValueFrom(
      this.memberApi.addFavorite(token, entry.isbn13),
    );
    if (!added.alreadyPresent) {
      this.currentError.set(null);
    }
    await this.refresh();
    return !added.alreadyPresent;
  }

  async remove(isbn13: string): Promise<void> {
    const entry = this.favorites().find(favorite => favorite.isbn13 === isbn13);
    if (!entry || this.pendingRemovals().has(isbn13)) {
      return;
    }

    const mode = this.mode();
    if (mode === 'local') {
      this.local.remove(isbn13);
      this.localRevision.update(value => value + 1);
    }

    const timer = setTimeout(() => {
      this.pendingRemovals.update(current => {
        const removal = current.get(isbn13);
        if (!removal) {
          return current;
        }
        const updated = new Map(current);
        updated.set(isbn13, {...removal, undoable: false});
        return updated;
      });
      void this.finalizeRemoval(isbn13);
    }, UNDO_DURATION_MS);
    this.pendingRemovals.update(pending => new Map(pending).set(isbn13, {
      entry,
      mode,
      timer,
      createdAt: Date.now(),
      undoable: true,
    }));
  }

  undoRemoval(): boolean {
    const pending = this.pendingRemovals();
    const latest = [...pending.entries()].filter(([, removal]) => removal.undoable).sort((left, right) =>
      right[1].createdAt - left[1].createdAt,
    )[0];
    if (!latest) {
      return false;
    }

    const [isbn13, removal] = latest;
    clearTimeout(removal.timer);
    const remaining = new Map(pending);
    remaining.delete(isbn13);
    this.pendingRemovals.set(remaining);
    if (removal.mode === 'local') {
      this.local.add(removal.entry);
      this.localRevision.update(value => value + 1);
    }
    return true;
  }

  async refresh(): Promise<void> {
    if (this.refreshInFlight) {
      return this.refreshInFlight;
    }

    const refresh = this.performRefresh();
    this.refreshInFlight = refresh;
    try {
      await refresh;
    } finally {
      if (this.refreshInFlight === refresh) {
        this.refreshInFlight = null;
      }
    }
  }

  async onSignedIn(): Promise<void> {
    if (!this.auth.isAuthenticated()) {
      return;
    }
    if (this.signInInFlight) {
      return this.signInInFlight;
    }

    this.signInInFlight = this.syncAfterSignIn();
    try {
      await this.signInInFlight;
    } catch (error: unknown) {
      this.currentError.set('Vos coups de cœur n’ont pas pu être synchronisés. La liste de ce navigateur est conservée.');
      throw error;
    } finally {
      this.signInInFlight = null;
    }
  }

  private async syncAfterSignIn(): Promise<void> {
    const entries = this.local.list();
    this.currentSyncNotice.set(null);
    this.currentError.set(null);
    const token = await this.auth.getApiAccessToken();
    if (entries.length > 0) {
      const result = await firstValueFrom(this.memberApi.mergeFavorites(
        token,
        entries.map(({isbn13, addedAt}) => ({isbn13, addedAt})),
      ));
      this.local.clear();
      this.localRevision.update(value => value + 1);
      this.currentSyncNotice.set(result);
    }

    try {
      const preference = await firstValueFrom(this.memberApi.getRecommendationPreference(token));
      const minimumHearts = entries.length > 0 ? this.local.minimumHearts() : preference.minimumHearts ?? 3;
      if (entries.length > 0) {
        await firstValueFrom(this.memberApi.setRecommendationPreference(
          token,
          preference.enabled,
          minimumHearts,
        ));
      }
      this.memberPreference.set({
        enabled: preference.enabled,
        minimumHearts,
        fairDigestConsentedAt: preference.fairDigestConsentedAt ?? null,
      });
      this.digestConsent.set(Boolean(preference.fairDigestConsentedAt));
    } catch {
      this.memberPreference.set(null);
      if (entries.length > 0) {
        this.currentError.set('Vos coups de cœur ont été repris, mais votre exigence de suggestions n’a pas pu être enregistrée.');
      }
    }

    await this.refresh();
  }

  private async performRefresh(): Promise<void> {
    if (this.auth.isAuthenticated()) {
      const token = await this.auth.getApiAccessToken();
      const response = await firstValueFrom(this.memberApi.getFavorites(token));
      this.remoteFavorites.set(response);
      this.localRevision.update(value => value + 1);
      this.updateComputingFromMemberFavorites(response);
      if (response.total === 0) {
        this.suggestionItems.set([]);
        this.currentSuggestionStatus.set('NoFavorites');
        this.stopPolling();
        return;
      }

      const suggestions = await firstValueFrom(this.memberApi.getRecommendations(
        token,
        8,
        this.minimumHearts(),
      ));
      this.suggestionItems.set(suggestions.items);
      this.currentSuggestionStatus.set(suggestions.status);
      this.updatePolling();
      return;
    }

    const entries = this.local.list();
    if (entries.length === 0) {
      this.suggestionItems.set([]);
      this.currentComputing.set({});
      this.currentSuggestionStatus.set('NoFavorites');
      this.stopPolling();
      return;
    }

    const suggestions = await firstValueFrom(this.api.getFavoriteSuggestions(
      entries.map(entry => entry.isbn13),
      this.local.minimumHearts(),
    ));
    this.suggestionItems.set(suggestions.items);
    this.currentSuggestionStatus.set(suggestions.status);
    this.currentComputing.set(Object.fromEntries(
      suggestions.seeds.map(seed => [seed.isbn13, isComputing(seed.status)]),
    ));
    this.updatePolling();
  }

  private updateComputingFromMemberFavorites(response: CatalogMemberFavoritesResponse): void {
    const computing: Record<string, boolean> = {};
    for (const work of response.works) {
      for (const edition of work.editions) {
        computing[edition.isbn13] = work.isComputingSuggestions || isComputing(edition.recommendationStatus);
      }
    }
    this.currentComputing.set(computing);
  }

  private updatePolling(): void {
    const hasComputingFavorites = Object.values(this.currentComputing()).some(Boolean);
    if (!hasComputingFavorites) {
      this.stopPolling();
      return;
    }

    const startedAt = this.refreshStartedAt() ?? Date.now();
    this.refreshStartedAt.set(startedAt);
    if (Date.now() - startedAt >= MAX_POLL_DURATION_MS || this.refreshTimer) {
      if (Date.now() - startedAt >= MAX_POLL_DURATION_MS) {
        this.stopPolling();
      }
      return;
    }

    this.refreshTimer = setTimeout(() => {
      this.refreshTimer = null;
      void this.refresh().catch(() => this.stopPolling());
    }, FAVORITE_REFRESH_INTERVAL_MS);
  }

  private stopPolling(): void {
    if (this.refreshTimer) {
      clearTimeout(this.refreshTimer);
      this.refreshTimer = null;
    }
    this.refreshStartedAt.set(null);
  }

  private localPendingRemovalCount(): number {
    return [...this.pendingRemovals().values()].filter(removal => removal.mode === 'local').length;
  }

  private remoteEntries(): LocalFavoriteEntry[] {
    return this.remoteFavorites()?.works.flatMap(work => work.editions.map(edition => ({
      isbn13: edition.isbn13,
      title: edition.title,
      authors: edition.authors,
      coverUrl: edition.coverUrl,
      addedAt: edition.addedAt,
      editionCount: work.editionCount,
      quantityAvailable: edition.quantityAvailable,
      quantityAnnounced: edition.quantityAnnounced,
    }))) ?? [];
  }

  private async finalizeRemoval(isbn13: string): Promise<void> {
    const pending = this.pendingRemovals().get(isbn13);
    if (!pending) {
      return;
    }

    try {
      if (pending.mode === 'local') {
        await firstValueFrom(this.api.withdrawAnonymousFavorite(isbn13));
      } else {
        const token = await this.auth.getApiAccessToken();
        await firstValueFrom(this.memberApi.removeFavorite(token, isbn13));
      }
      this.removePending(isbn13);
      await this.refresh();
    } catch {
      this.removePending(isbn13);
      if (pending.mode === 'local') {
        this.local.add(pending.entry);
        this.localRevision.update(value => value + 1);
      }
      this.currentError.set('La suppression du coup de cœur a échoué. Votre livre a été rétabli.');
      await this.refresh().catch(() => undefined);
    }
  }

  private removePending(isbn13: string): void {
    this.pendingRemovals.update(pending => {
      const remaining = new Map(pending);
      remaining.delete(isbn13);
      return remaining;
    });
  }
}

function isComputing(status: string): boolean {
  return status.toLowerCase() === 'computing';
}
