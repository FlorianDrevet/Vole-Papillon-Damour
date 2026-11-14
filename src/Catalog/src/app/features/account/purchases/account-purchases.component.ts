import {ChangeDetectionStrategy, Component, OnInit, signal} from '@angular/core';
import {firstValueFrom} from 'rxjs';

import {CatalogAuthenticationRedirectStartedError, CatalogAuthService} from '../../../core/catalog-auth.service';
import {CatalogMemberApiService} from '../../../core/catalog-member-api.service';
import {CatalogFavoritesService} from '../../../core/favorites/catalog-favorites.service';
import {CatalogPurchaseLine, CatalogPurchasePassage, CatalogPurchasesResponse} from '../../../core/catalog.models';

const CONTACT_EMAIL = 'volepapillondamour@sfr.fr';

@Component({
  selector: 'app-account-purchases',
  standalone: false,
  templateUrl: './account-purchases.component.html',
  styleUrls: ['./account-purchases.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AccountPurchasesComponent implements OnInit {
  readonly purchases = signal<CatalogPurchasesResponse | null>(null);
  readonly loading = signal(false);
  readonly loadingMore = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly favoriteError = signal<string | null>(null);
  readonly favoritePendingIsbns = signal<ReadonlySet<string>>(new Set());
  readonly reportingPassageId = signal<string | null>(null);
  readonly contactEmail = CONTACT_EMAIL;

  constructor(
    private readonly auth: CatalogAuthService,
    private readonly api: CatalogMemberApiService,
    readonly favorites: CatalogFavoritesService,
  ) {}

  ngOnInit(): void {
    void this.loadPurchases();
  }

  async loadPurchases(): Promise<void> {
    if (this.loading()) {
      return;
    }

    this.loading.set(true);
    this.errorMessage.set(null);
    try {
      const token = await this.auth.getApiAccessToken();
      this.purchases.set(await firstValueFrom(this.api.getPurchases(token)));
    } catch (error: unknown) {
      this.purchases.set(null);
      this.errorMessage.set(this.errorFor(error));
    } finally {
      this.loading.set(false);
    }
  }

  async loadOlderPurchases(): Promise<void> {
    const current = this.purchases();
    if (!current?.nextCursor || this.loadingMore()) {
      return;
    }

    this.loadingMore.set(true);
    this.errorMessage.set(null);
    try {
      const token = await this.auth.getApiAccessToken();
      const next = await firstValueFrom(this.api.getPurchases(token, current.nextCursor));
      this.purchases.set({
        passages: [...current.passages, ...next.passages],
        nextCursor: next.nextCursor,
      });
    } catch (error: unknown) {
      this.errorMessage.set(this.errorFor(error));
    } finally {
      this.loadingMore.set(false);
    }
  }

  passageHeading(passage: CatalogPurchasePassage): string {
    const date = new Intl.DateTimeFormat('fr-FR', {
      day: 'numeric',
      month: 'long',
      year: 'numeric',
    }).format(new Date(passage.occurredAt));
    const fairName = passage.fairLabel?.split(' · ', 1)[0];
    return fairName ? `${date} · ${fairName}` : date;
  }

  passageCount(passage: CatalogPurchasePassage): string {
    return `${passage.activeBookCount} livre${passage.activeBookCount > 1 ? 's' : ''}`;
  }

  lineHeading(line: CatalogPurchaseLine): string {
    return [line.title, line.publisher, line.publicationYear]
      .filter(value => value !== null && value !== undefined && value !== '')
      .join(' · ');
  }

  toggleReport(passageId: string): void {
    this.reportingPassageId.update(current => current === passageId ? null : passageId);
  }

  isFavorite(line: CatalogPurchaseLine): boolean {
    return Boolean(line.isbn13 && this.favorites.favoriteIsbns().has(line.isbn13));
  }

  favoriteButtonLabel(line: CatalogPurchaseLine): string {
    return this.isFavorite(line)
      ? `Retirer ${line.title} de mes coups de cœur`
      : `J'ai aimé ${line.title}`;
  }

  async toggleFavorite(line: CatalogPurchaseLine): Promise<void> {
    const isbn13 = line.isbn13;
    if (!isbn13 || this.favoritePendingIsbns().has(isbn13)) {
      return;
    }

    this.favoriteError.set(null);
    this.favoritePendingIsbns.update(current => new Set(current).add(isbn13));
    try {
      if (this.isFavorite(line)) {
        await this.favorites.remove(isbn13);
      } else {
        await this.favorites.add({
          isbn13,
          title: line.title,
          authors: line.authors,
          coverUrl: line.currentCoverUrl,
        });
      }
    } catch {
      this.favoriteError.set('Ce livre n’a pas pu être ajouté à vos coups de cœur. Réessayez.');
    } finally {
      this.favoritePendingIsbns.update(current => {
        const next = new Set(current);
        next.delete(isbn13);
        return next;
      });
    }
  }

  private errorFor(error: unknown): string {
    return error instanceof CatalogAuthenticationRedirectStartedError
      ? 'Redirection vers votre fournisseur de connexion pour renouveler votre session…'
      : 'Mes achats n’ont pas pu être chargés. Réessayez.';
  }
}
