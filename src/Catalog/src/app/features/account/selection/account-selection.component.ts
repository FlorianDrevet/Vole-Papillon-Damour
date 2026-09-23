import {ChangeDetectionStrategy, Component, EventEmitter, Output, computed, input, signal} from '@angular/core';
import {firstValueFrom} from 'rxjs';

import {CatalogAuthService} from '../../../core/catalog-auth.service';
import {CatalogMemberApiService} from '../../../core/catalog-member-api.service';
import {
  CatalogSelectionAvailability,
  CatalogSelectionItem,
  CatalogSelectionStatus,
} from '../../../core/catalog.models';
import {CatalogSelectionService} from '../../../core/selection/catalog-selection.service';
import {LocalSelectionEntry, SelectionRef, selectionKey} from '../../../core/selection/selection-merge';
import {LocalSelectionStore} from '../../../core/selection/local-selection.store';

type SelectionFilter = 'all' | 'next' | 'available' | 'purchased' | 'not-found' | 'unavailable';

interface SelectionDisplayItem {
  key: string;
  id: string | null;
  ref: SelectionRef;
  title: string;
  addedAt: string;
  remote: CatalogSelectionItem | null;
}

const FILTERS: ReadonlyArray<{id: SelectionFilter; label: string}> = [
  {id: 'all', label: 'Tout'},
  {id: 'next', label: 'Prochaine visite'},
  {id: 'available', label: 'Encore disponible'},
  {id: 'purchased', label: 'Acheté'},
  {id: 'not-found', label: 'Pas trouvé'},
  {id: 'unavailable', label: 'Indisponibles'},
];

const STATUSES: ReadonlyArray<{value: CatalogSelectionStatus; label: string; testId: string}> = [
  {value: 'ToTake', label: 'À prendre', testId: 'take'},
  {value: 'Purchased', label: 'Acheté', testId: 'purchased'},
  {value: 'NotFound', label: 'Pas trouvé', testId: 'not-found'},
  {value: 'ToRevisit', label: 'À revoir', testId: 'revisit'},
];

@Component({
  selector: 'app-account-selection',
  standalone: false,
  templateUrl: './account-selection.component.html',
  styleUrls: ['./account-selection.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AccountSelectionComponent {
  readonly selectionLoading = input(false);
  readonly selectionError = input<string | null>(null);
  @Output() retryRequested = new EventEmitter<void>();

  readonly filters = FILTERS;
  readonly statuses = STATUSES;
  readonly filter = signal<SelectionFilter>('all');
  readonly busyItemId = signal<string | null>(null);
  readonly busyMerge = signal(false);
  readonly errorMessage = signal<string | null>(null);
  private readonly localRevision = signal(0);
  readonly pendingMerge = computed(() => this.selection.pendingMerge());
  readonly mode = computed(() => this.selection.mode());
  readonly localEntries = computed(() => {
    this.selection.mode();
    this.selection.keys();
    this.selection.snapshot();
    this.localRevision();
    return this.local.list();
  });
  readonly remoteItems = computed(() => this.selection.snapshot()?.items ?? []);
  readonly allItems = computed(() => {
    const remote = this.remoteItems().flatMap(item => {
      const ref = this.selectionRef(item);
      if (!ref) {
        return [];
      }

      return [{
        key: selectionKey(ref),
        id: item.id,
        ref,
        title: item.title,
        addedAt: item.addedAt,
        remote: item,
      } satisfies SelectionDisplayItem];
    });
    const remoteKeys = new Set(remote.map(item => item.key));
    const local = this.localEntries()
      .filter(entry => !remoteKeys.has(selectionKey(entry.ref)))
      .map(entry => this.localDisplayItem(entry));

    if (!this.auth.isAuthenticated()) {
      return local;
    }

    return [...remote, ...local];
  });
  readonly visibleItems = computed(() => this.allItems().filter(item => this.matchesFilter(item, this.filter())));
  readonly isLoading = computed(() => this.selectionLoading() ||
    (this.auth.isAuthenticated() && this.selection.snapshot() === null && !this.selectionError()));
  readonly localUnsynced = computed(() => this.mode() === 'local-unsynced');
  readonly hasItems = computed(() => this.allItems().length > 0);
  readonly localItemsToAdd = computed(() => {
    const remoteKeys = new Set(this.remoteItems().map(item => {
      const ref = this.selectionRef(item);
      return ref ? selectionKey(ref) : '';
    }));
    return this.localEntries().filter(entry => !remoteKeys.has(selectionKey(entry.ref)));
  });

  constructor(
    private readonly auth: CatalogAuthService,
    private readonly api: CatalogMemberApiService,
    private readonly selection: CatalogSelectionService,
    private readonly local: LocalSelectionStore,
  ) {}

  isAuthenticated(): boolean {
    return this.auth.isAuthenticated();
  }

  selectFilter(filter: SelectionFilter): void {
    this.filter.set(filter);
  }

  changeFilter(event: Event): void {
    const value = (event.target as HTMLSelectElement).value;
    if (FILTERS.some(option => option.id === value)) {
      this.filter.set(value as SelectionFilter);
    }
  }

  filterCount(filter: SelectionFilter): number {
    return this.allItems().filter(item => this.matchesFilter(item, filter)).length;
  }

  availabilityLabel(availability: CatalogSelectionAvailability): string {
    return ({
      Available: 'Disponible',
      Announced: 'Annoncé',
      OutOfStock: 'Épuisé',
      RareSold: 'Fiche vendue',
      Unavailable: 'Plus visible au catalogue',
    } satisfies Record<CatalogSelectionAvailability, string>)[availability];
  }

  availabilityClass(availability: CatalogSelectionAvailability): string {
    return ({
      Available: 'available',
      Announced: 'announced',
      OutOfStock: 'exhausted',
      RareSold: 'rare-sold',
      Unavailable: 'unavailable',
    } satisfies Record<CatalogSelectionAvailability, string>)[availability];
  }

  editionDetails(item: CatalogSelectionItem): string {
    return [item.publisher, item.publicationYear, item.physicalFormat]
      .filter(value => value !== null && value !== undefined && value !== '')
      .join(' · ');
  }

  formatDate(value: string): string {
    return new Intl.DateTimeFormat('fr-FR', {
      day: 'numeric',
      month: 'long',
      year: 'numeric',
      timeZone: 'Europe/Paris',
    }).format(new Date(value));
  }

  formatShortDate(value: string): string {
    return new Intl.DateTimeFormat('fr-FR', {
      day: 'numeric',
      month: 'long',
      timeZone: 'Europe/Paris',
    }).format(new Date(value));
  }

  freshnessLine(): string | null {
    const generatedAt = this.selection.snapshot()?.generatedAt;
    if (!generatedAt) {
      return null;
    }

    const date = new Date(generatedAt);
    const day = new Intl.DateTimeFormat('fr-FR', {
      day: 'numeric',
      month: 'long',
      year: 'numeric',
      timeZone: 'Europe/Paris',
    }).format(date);
    const time = new Intl.DateTimeFormat('fr-FR', {
      hour: '2-digit',
      minute: '2-digit',
      timeZone: 'Europe/Paris',
    }).format(date);
    return `Disponibilité vérifiée le ${day} à ${time}. Elle peut changer avant votre visite.`;
  }

  nextFairLabel(): string | null {
    const startsAt = this.selection.snapshot()?.nextFair?.startsAt;
    return startsAt ? `prochaine bourse : ${this.formatShortDate(startsAt)}` : null;
  }

  addedLabel(item: SelectionDisplayItem): string {
    if (item.remote?.status === 'Purchased' && item.remote.purchasedAt) {
      return `Acheté le ${this.formatDate(item.remote.purchasedAt)}`;
    }

    return item.remote?.kind === 'rare'
      ? `Posé par vous le ${this.formatDate(item.addedAt)}`
      : `Ajouté le ${this.formatDate(item.addedAt)}`;
  }

  isSelectedStatus(item: SelectionDisplayItem, status: CatalogSelectionStatus): boolean {
    return item.remote?.status === status;
  }

  async setStatus(item: SelectionDisplayItem, status: CatalogSelectionStatus): Promise<void> {
    const remote = item.remote;
    if (!remote || !this.auth.isAuthenticated() || remote.status === status || this.busyItemId()) {
      return;
    }

    this.busyItemId.set(remote.id);
    this.errorMessage.set(null);
    try {
      const token = await this.auth.getApiAccessToken();
      await firstValueFrom(this.api.setSelectionStatus(token, remote.id, status));
      await this.selection.refresh();
    } catch {
      this.errorMessage.set('L’état de Ma sélection n’a pas pu être modifié. Réessayez.');
    } finally {
      this.busyItemId.set(null);
    }
  }

  async removeItem(item: SelectionDisplayItem): Promise<void> {
    if (this.busyItemId()) {
      return;
    }

    this.busyItemId.set(item.id ?? item.key);
    this.errorMessage.set(null);
    try {
      if (item.remote && this.auth.isAuthenticated()) {
        const token = await this.auth.getApiAccessToken();
        await firstValueFrom(this.api.removeSelectionItem(token, item.remote.id));
        await this.selection.refresh();
      } else if (this.auth.isAuthenticated()) {
        this.local.remove(item.ref);
        this.localRevision.update(revision => revision + 1);
      } else {
        await this.selection.remove(item.ref);
      }
    } catch {
      this.errorMessage.set('Ce livre n’a pas pu être retiré de Ma sélection. Réessayez.');
    } finally {
      this.busyItemId.set(null);
    }
  }

  async confirmMerge(): Promise<void> {
    if (this.busyMerge()) {
      return;
    }

    this.busyMerge.set(true);
    this.errorMessage.set(null);
    try {
      await this.selection.confirmMerge();
    } catch {
      this.errorMessage.set('La fusion n’a pas abouti. Votre sélection sur cet appareil est conservée. Réessayez.');
    } finally {
      this.busyMerge.set(false);
    }
  }

  declineMerge(): void {
    this.selection.declineMerge();
  }

  retryMerge(): void {
    this.errorMessage.set(null);
    this.retryRequested.emit();
  }

  async login(): Promise<void> {
    this.errorMessage.set(null);
    try {
      await this.auth.login('/compte');
    } catch {
      this.errorMessage.set('La connexion n’a pas pu être démarrée. Réessayez.');
    }
  }

  trackByKey(_index: number, item: SelectionDisplayItem): string {
    return item.key;
  }

  selectionKeyForLocal(entry: LocalSelectionEntry): string {
    return selectionKey(entry.ref);
  }

  private matchesFilter(item: SelectionDisplayItem, filter: SelectionFilter): boolean {
    const remote = item.remote;
    if (!remote) {
      return filter === 'all' || filter === 'next';
    }

    switch (filter) {
      case 'all':
        return true;
      case 'next':
        return remote.status === 'ToTake' || remote.status === 'ToRevisit';
      case 'available':
        return remote.availability === 'Available' || remote.availability === 'Announced';
      case 'purchased':
        return remote.status === 'Purchased';
      case 'not-found':
        return remote.status === 'NotFound';
      case 'unavailable':
        return remote.availability === 'OutOfStock' ||
          remote.availability === 'RareSold' ||
          remote.availability === 'Unavailable';
    }
  }

  private selectionRef(item: CatalogSelectionItem): SelectionRef | null {
    if (item.kind === 'edition' && item.isbn13) {
      return {kind: 'edition', isbn13: item.isbn13};
    }

    if (item.kind === 'rare' && item.rareBookId) {
      return {kind: 'rare', rareBookId: item.rareBookId};
    }

    return null;
  }

  private localDisplayItem(entry: LocalSelectionEntry): SelectionDisplayItem {
    return {
      key: selectionKey(entry.ref),
      id: null,
      ref: entry.ref,
      title: entry.title,
      addedAt: entry.addedAt,
      remote: null,
    };
  }
}
