import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  OnDestroy,
  OnInit,
} from '@angular/core';
import {HttpErrorResponse} from '@angular/common/http';
import {ActivatedRoute, Router} from '@angular/router';
import {Subject, catchError, firstValueFrom, of, takeUntil} from 'rxjs';

import {CatalogApiService} from '../../core/catalog-api.service';
import {mergeCatalogGenres} from '../../core/catalog-genres';
import {
  CatalogAvailability,
  CatalogBookReference,
  CatalogSearchParams,
  CatalogSearchResponse,
  CatalogSort,
  CatalogReferenceSearchResponse,
  CatalogWatchlistScope,
} from '../../core/catalog.models';
import {CatalogAuthService} from '../../core/catalog-auth.service';
import {CatalogMemberApiService} from '../../core/catalog-member-api.service';

@Component({
  selector: 'app-catalog-search-page',
  standalone: false,
  templateUrl: './catalog-search-page.component.html',
  styleUrls: ['./catalog-search-page.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CatalogSearchPageComponent implements OnInit, OnDestroy {
  query = '';
  genre = '';
  availability: CatalogAvailability = 'all';
  rareOnly = false;
  sort: CatalogSort = 'relevance';
  browseMode = false;
  loading = true;
  error = false;
  response: CatalogSearchResponse | null = null;
  externalLoading = false;
  externalError = false;
  externalResponse: CatalogReferenceSearchResponse | null = null;
  referenceFollowPending: string | null = null;
  referenceFollowMessage: string | null = null;
  referenceFollowError: string | null = null;

  private readonly destroyed = new Subject<void>();
  private readonly pendingReferenceStorageKey = 'vpd.catalog.pending-reference-follow';

  constructor(
    private readonly route: ActivatedRoute,
    private readonly router: Router,
    private readonly api: CatalogApiService,
    private readonly auth: CatalogAuthService,
    private readonly memberApi: CatalogMemberApiService,
    private readonly changeDetector: ChangeDetectorRef,
  ) {}

  ngOnInit(): void {
    this.browseMode = this.route.snapshot.data['browse'] === true;
    this.route.queryParamMap
      .pipe(takeUntil(this.destroyed))
      .subscribe(params => {
        this.query = params.get('q') || '';
        this.genre = params.get('genre') || '';
        this.availability = this.readAvailability(params.get('availability'));
        this.rareOnly = params.get('rare') === 'true';
        this.sort = params.get('sort') === 'recent' ? 'recent' : 'relevance';
        this.load();
      });
    void this.restorePendingReferenceFollow();
  }

  ngOnDestroy(): void {
    this.destroyed.next();
    this.destroyed.complete();
  }

  submitSearch(): void {
    void this.router.navigate(['/recherche'], {queryParams: this.queryParams()});
  }

  clearSearch(): void {
    this.query = '';
    this.submitSearch();
  }

  applyFilters(): void {
    void this.router.navigate([this.browseMode ? '/catalogue' : '/recherche'], {
      queryParams: this.queryParams(),
    });
  }

  clearFilters(): void {
    this.query = '';
    this.genre = '';
    this.availability = 'all';
    this.rareOnly = false;
    this.sort = 'relevance';
    this.applyFilters();
  }

  goToPage(page: number): void {
    if (page < 1 || !this.response || page > this.totalPages()) {
      return;
    }

    void this.router.navigate([this.browseMode ? '/catalogue' : '/recherche'], {
      queryParams: {...this.queryParams(), page},
    });
  }

  goToExternalPage(page: number): void {
    if (page < 1 || !this.externalResponse || page < 1) {
      return;
    }

    void this.router.navigate([this.browseMode ? '/catalogue' : '/recherche'], {
      queryParams: {...this.queryParams(), referencePage: page},
    });
  }

  async followReference(
    item: CatalogBookReference,
    scope: CatalogWatchlistScope = 'Edition',
  ): Promise<void> {
    const key = this.referenceFollowKey(item, scope);
    if (!key || this.referenceFollowPending) {
      return;
    }

    this.referenceFollowMessage = null;
    this.referenceFollowError = null;

    if (!this.auth.isAuthenticated()) {
      this.savePendingReferenceFollow(item, scope);
      try {
        await this.auth.login(this.referenceReturnUrl());
      } catch {
        this.clearPendingReferenceFollow();
        this.referenceFollowError = 'La connexion n’a pas pu être démarrée. Réessayez.';
        this.changeDetector.markForCheck();
      }
      return;
    }

    await this.submitReferenceFollow(item, scope, key);
  }

  referenceFollowKey(item: CatalogBookReference, scope: CatalogWatchlistScope): string | null {
    const target = scope === 'Work' ? item.workId : item.isbn13;
    return target ? `${scope}:${target}` : null;
  }

  workReference(): CatalogBookReference | null {
    const items = this.externalResponse?.items ?? [];
    const referencesWithWork = items.filter(item => item.workId);
    const workIds = new Set(referencesWithWork.map(item => item.workId));
    return workIds.size === 1 ? referencesWithWork[0] : null;
  }

  private async submitReferenceFollow(
    item: CatalogBookReference,
    scope: CatalogWatchlistScope,
    key: string,
  ): Promise<void> {
    this.referenceFollowPending = key;
    try {
      const token = await this.auth.getApiAccessToken();
      const request = this.followRequest(item, scope);
      if (!request) {
        this.referenceFollowError = 'Cette référence ne permet pas ce type de suivi.';
        return;
      }

      await firstValueFrom(this.memberApi.addWatchlistItem(token, request));
      this.referenceFollowMessage = scope === 'Work'
        ? 'Le titre a été ajouté à votre liste de recherche.'
        : 'L’édition a été ajoutée à votre liste de recherche.';
    } catch (error: unknown) {
      this.referenceFollowError = error instanceof HttpErrorResponse && error.status === 409
        ? 'Ce titre est déjà présent dans votre liste de recherche, ou votre liste est pleine.'
        : 'Le titre n’a pas pu être ajouté. Réessayez dans un instant.';
    } finally {
      this.referenceFollowPending = null;
      this.changeDetector.markForCheck();
    }
  }

  referenceEditionLabel(item: CatalogBookReference): string {
    const details = [item.publisher, item.publicationYear ? String(item.publicationYear) : null]
      .filter((value): value is string => Boolean(value));
    return details.join(' · ') || (item.isbn13 ? `ISBN ${item.isbn13}` : 'Édition repérée');
  }

  resultHeading(): string {
    const trimmedQuery = this.query.trim();
    if (trimmedQuery) {
      return `Résultats pour « ${trimmedQuery} »`;
    }

    return this.browseMode ? 'Le catalogue complet' : 'Tous les livres du catalogue';
  }

  localCountLabel(): string {
    const total = this.response?.totalCount || 0;
    return `${total} ${total === 1 ? 'édition' : 'éditions'}`;
  }

  externalCountLabel(): string {
    const total = this.externalResponse?.items.length || 0;
    return `${total} ${total === 1 ? 'édition' : 'éditions'}`;
  }

  totalPages(): number {
    if (!this.response || this.response.totalCount === 0) {
      return 1;
    }
    return Math.ceil(this.response.totalCount / this.response.pageSize);
  }

  pageNumbers(): number[] {
    const total = this.totalPages();
    const current = this.response?.page || 1;
    const first = Math.max(1, Math.min(current - 2, total - 4));
    const last = Math.min(total, first + 4);
    return Array.from({length: last - first + 1}, (_, index) => first + index);
  }

  resultSummary(): string {
    const total = this.response?.totalCount || 0;
    if (total === 0) {
      return 'Aucun titre trouvé dans la bourse aux livres';
    }
    return `${total} ${total === 1 ? 'titre trouvé' : 'titres trouvés'} dans la bourse aux livres`;
  }

  availableGenres(): string[] {
    return mergeCatalogGenres(this.response?.genres, this.genre);
  }

  visibleGenres(): string[] {
    const genres = this.availableGenres();
    const visible = genres.slice(0, 4);
    if (this.genre && !visible.includes(this.genre)) {
      visible.unshift(this.genre);
    }
    return visible;
  }

  trackBook(_index: number, isbn13: string): string {
    return isbn13;
  }

  trackReference(index: number, reference: CatalogBookReference): string {
    return `${reference.isbn13 || reference.workId || reference.title || 'reference'}-${index}`;
  }

  private load(): void {
    this.loading = true;
    this.error = false;
    this.api.search(this.searchParams())
      .pipe(
        catchError(() => {
          this.error = true;
          return of(null);
        }),
        takeUntil(this.destroyed),
      )
      .subscribe(response => {
        this.response = response;
        this.loading = false;
        this.changeDetector.markForCheck();
      });

    const referenceQuery = this.query.trim();
    if (referenceQuery.length < 2) {
      this.externalResponse = null;
      this.externalLoading = false;
      this.externalError = false;
      return;
    }

    this.externalLoading = true;
    this.externalError = false;
    this.api.searchReferences(referenceQuery, this.readReferencePage(), 20)
      .pipe(
        catchError(() => {
          this.externalError = true;
          return of(null);
        }),
        takeUntil(this.destroyed),
      )
      .subscribe(response => {
        this.externalResponse = response;
        this.externalLoading = false;
        this.changeDetector.markForCheck();
      });
  }

  private searchParams(): CatalogSearchParams {
    return {
      query: this.query,
      genre: this.genre,
      availability: this.availability,
      rareOnly: this.rareOnly,
      sort: this.sort,
      page: this.readPage(),
      pageSize: 24,
    };
  }

  private queryParams(): Record<string, string | number | boolean> {
    const params: Record<string, string | number | boolean> = {};
    if (this.query.trim()) params['q'] = this.query.trim();
    if (this.genre.trim()) params['genre'] = this.genre.trim();
    if (this.availability !== 'all') params['availability'] = this.availability;
    if (this.rareOnly) params['rare'] = true;
    if (this.sort !== 'relevance') params['sort'] = this.sort;
    if (this.readReferencePage() > 1) params['referencePage'] = this.readReferencePage();
    return params;
  }

  private readPage(): number {
    const page = Number(this.route.snapshot.queryParamMap.get('page') || '1');
    return Number.isInteger(page) && page > 0 ? page : 1;
  }

  private readAvailability(value: string | null): CatalogAvailability {
    return value === 'available' || value === 'next' ? value : 'all';
  }

  private readReferencePage(): number {
    const page = Number(this.route.snapshot.queryParamMap.get('referencePage') || '1');
    return Number.isInteger(page) && page > 0 ? page : 1;
  }

  private referenceReturnUrl(): string {
    const query = this.queryParams();
    const search = new URLSearchParams();
    for (const [key, value] of Object.entries(query)) {
      search.set(key, String(value));
    }
    return `/recherche${search.toString() ? `?${search.toString()}` : ''}`;
  }

  private followRequest(item: CatalogBookReference, scope: CatalogWatchlistScope): {
    scope: CatalogWatchlistScope;
    workId: string | null;
    isbn13: string | null;
  } | null {
    if (scope === 'Work' && item.workId) {
      return {scope: 'Work', workId: item.workId, isbn13: null};
    }

    if (scope === 'Edition' && item.isbn13) {
      return {scope: 'Edition', workId: null, isbn13: item.isbn13};
    }

    return null;
  }

  private async restorePendingReferenceFollow(): Promise<void> {
    const pending = this.readPendingReferenceFollow();
    if (!pending) {
      return;
    }

    try {
      await this.auth.initialize();
    } catch {
      return;
    }

    if (!this.auth.isAuthenticated()) {
      return;
    }

    this.clearPendingReferenceFollow();
    const key = this.referenceFollowKey(pending.item, pending.scope);
    if (key) {
      await this.submitReferenceFollow(pending.item, pending.scope, key);
    }
  }

  private savePendingReferenceFollow(item: CatalogBookReference, scope: CatalogWatchlistScope): void {
    try {
      if (typeof sessionStorage !== 'undefined') {
        sessionStorage.setItem(
          this.pendingReferenceStorageKey,
          JSON.stringify({item, scope}),
        );
      }
    } catch {
      // A blocked session storage should not prevent the normal login redirect.
    }
  }

  private readPendingReferenceFollow(): {
    item: CatalogBookReference;
    scope: CatalogWatchlistScope;
  } | null {
    try {
      if (typeof sessionStorage === 'undefined') {
        return null;
      }

      const raw = sessionStorage.getItem(this.pendingReferenceStorageKey);
      if (!raw) {
        return null;
      }

      const pending = JSON.parse(raw) as {
        item?: CatalogBookReference;
        scope?: CatalogWatchlistScope;
      };
      if (!pending.item || (pending.scope !== 'Work' && pending.scope !== 'Edition')) {
        this.clearPendingReferenceFollow();
        return null;
      }

      return {item: pending.item, scope: pending.scope};
    } catch {
      this.clearPendingReferenceFollow();
      return null;
    }
  }

  private clearPendingReferenceFollow(): void {
    try {
      if (typeof sessionStorage !== 'undefined') {
        sessionStorage.removeItem(this.pendingReferenceStorageKey);
      }
    } catch {
      // Ignore storage cleanup failures; the current interaction remains usable.
    }
  }

}
