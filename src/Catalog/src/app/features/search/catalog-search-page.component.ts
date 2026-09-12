import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  ElementRef,
  HostListener,
  OnDestroy,
  OnInit,
} from '@angular/core';
import {HttpErrorResponse} from '@angular/common/http';
import {ActivatedRoute, ParamMap, Router} from '@angular/router';
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

interface CatalogSearchRouteState {
  query: string;
  genre: string;
  availability: CatalogAvailability;
  rareOnly: boolean;
  sort: CatalogSort;
  page: number;
  referencePage: number;
}

@Component({
  selector: 'app-catalog-search-page',
  standalone: false,
  templateUrl: './catalog-search-page.component.html',
  styleUrls: ['./catalog-search-page.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CatalogSearchPageComponent implements OnInit, OnDestroy {
  query = '';
  submittedQuery = '';
  genre = '';
  availability: CatalogAvailability = 'all';
  rareOnly = false;
  sort: CatalogSort = 'relevance';
  sortMenuOpen = false;
  sortMenuActiveIndex = 0;
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

  readonly sortChoices: ReadonlyArray<{
    value: CatalogSort;
    label: string;
    description: string;
  }> = [
    {
      value: 'relevance',
      label: 'Pertinence',
      description: 'Titres proches de votre recherche',
    },
    {
      value: 'recent',
      label: 'Arrivée récente',
      description: 'Titres ajoutés récemment',
    },
  ];

  private readonly destroyed = new Subject<void>();
  private readonly pendingReferenceStorageKey = 'vpd.catalog.pending-reference-follow';
  private routeState: CatalogSearchRouteState | null = null;
  private currentPage = 1;
  private currentReferencePage = 1;
  private localLoadVersion = 0;
  private externalLoadVersion = 0;

  constructor(
    private readonly route: ActivatedRoute,
    private readonly router: Router,
    private readonly api: CatalogApiService,
    private readonly auth: CatalogAuthService,
    private readonly memberApi: CatalogMemberApiService,
    private readonly changeDetector: ChangeDetectorRef,
    private readonly elementRef: ElementRef<HTMLElement>,
  ) {}

  ngOnInit(): void {
    this.browseMode = this.route.snapshot.data['browse'] === true;
    this.route.queryParamMap
      .pipe(takeUntil(this.destroyed))
      .subscribe(params => {
        const nextState = this.readRouteState(params);
        const previousState = this.routeState;
        this.query = nextState.query;
        this.submittedQuery = nextState.query;
        this.genre = nextState.genre;
        this.availability = nextState.availability;
        this.rareOnly = nextState.rareOnly;
        this.sort = nextState.sort;
        this.currentPage = nextState.page;
        this.currentReferencePage = nextState.referencePage;
        this.sortMenuOpen = false;
        this.routeState = nextState;

        if (!previousState || this.localRouteStateChanged(previousState, nextState)) {
          this.loadLocalResults();
        }
        if (!previousState || this.externalRouteStateChanged(previousState, nextState)) {
          this.loadExternalResults();
        }
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

  sortLabel(): string {
    return this.sortChoices.find(choice => choice.value === this.sort)?.label ?? 'Pertinence';
  }

  toggleSortMenu(): void {
    if (this.sortMenuOpen) {
      this.closeSortMenu();
      return;
    }

    this.openSortMenu();
  }

  selectSort(value: CatalogSort): void {
    this.sort = value;
    this.sortMenuActiveIndex = this.sortChoices.findIndex(choice => choice.value === value);
    this.closeSortMenu(true);
    this.applyFilters();
    this.changeDetector.markForCheck();
  }

  handleSortTriggerKeydown(event: KeyboardEvent): void {
    switch (event.key) {
      case 'Enter':
      case ' ':
        event.preventDefault();
        this.toggleSortMenu();
        break;
      case 'ArrowDown':
        event.preventDefault();
        this.openSortMenu();
        this.moveSortMenuFocus(1);
        break;
      case 'ArrowUp':
        event.preventDefault();
        this.openSortMenu();
        this.moveSortMenuFocus(-1);
        break;
      case 'Escape':
        if (this.sortMenuOpen) {
          event.preventDefault();
          this.closeSortMenu();
        }
        break;
    }
  }

  handleSortOptionKeydown(event: KeyboardEvent, index: number): void {
    switch (event.key) {
      case 'Enter':
      case ' ':
        event.preventDefault();
        this.selectSort(this.sortChoices[index].value);
        break;
      case 'ArrowDown':
        event.preventDefault();
        this.moveSortMenuFocus(1);
        break;
      case 'ArrowUp':
        event.preventDefault();
        this.moveSortMenuFocus(-1);
        break;
      case 'Home':
        event.preventDefault();
        this.sortMenuActiveIndex = 0;
        this.focusSortOption(this.sortMenuActiveIndex);
        break;
      case 'End':
        event.preventDefault();
        this.sortMenuActiveIndex = this.sortChoices.length - 1;
        this.focusSortOption(this.sortMenuActiveIndex);
        break;
      case 'Escape':
        event.preventDefault();
        event.stopPropagation();
        this.closeSortMenu(true);
        break;
      case 'Tab':
        this.closeSortMenu();
        break;
    }
  }

  @HostListener('document:click', ['$event'])
  closeSortMenuOnDocumentClick(event: MouseEvent): void {
    if (this.sortMenuOpen && !this.elementRef.nativeElement.contains(event.target as Node)) {
      this.closeSortMenu();
    }
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
      queryParams: {...this.queryParams(true, this.submittedQuery), page},
    });
  }

  goToExternalPage(page: number): void {
    if (page < 1 || !this.externalResponse) {
      return;
    }

    void this.router.navigate([this.browseMode ? '/catalogue' : '/recherche'], {
      queryParams: {...this.queryParams(true, this.submittedQuery), referencePage: page},
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
      this.savePendingReferenceFollow([item], scope);
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
    return this.workReferences()[0] ?? null;
  }

  private workReferences(): CatalogBookReference[] {
    const items = this.externalResponse?.items ?? [];
    if (!items.length) {
      return [];
    }

    const groups = new Map<string, CatalogBookReference[]>();
    for (const item of items) {
      const key = this.referenceTitleKey(item);
      const group = groups.get(key) ?? [];
      group.push(item);
      groups.set(key, group);
    }

    const rankedGroups = [...groups.values()]
      .sort((left, right) => right.length - left.length);
    if (
      !rankedGroups[0] ||
      (rankedGroups[1] && rankedGroups[0].length === rankedGroups[1].length)
    ) {
      return [];
    }

    const references = rankedGroups[0].filter(item => Boolean(item.workId?.trim()));
    const seenWorkIds = new Set<string>();
    return references.filter(item => {
      const workId = item.workId?.trim().toLowerCase();
      if (!workId) {
        return false;
      }

      if (seenWorkIds.has(workId)) {
        return false;
      }

      seenWorkIds.add(workId);
      return true;
    });
  }

  workFollowKey(): string | null {
    return this.workFollowKeyFor(this.workReferences());
  }

  workFollowPending(): boolean {
    const key = this.workFollowKey();
    return key !== null && this.referenceFollowPending === key;
  }

  async followWork(): Promise<void> {
    const references = this.workReferences();
    const key = this.workFollowKeyFor(references);
    if (!key || this.referenceFollowPending) {
      return;
    }

    this.referenceFollowMessage = null;
    this.referenceFollowError = null;

    if (!this.auth.isAuthenticated()) {
      this.savePendingReferenceFollow(references, 'Work');
      try {
        await this.auth.login(this.referenceReturnUrl());
      } catch {
        this.clearPendingReferenceFollow();
        this.referenceFollowError = 'La connexion n’a pas pu être démarrée. Réessayez.';
        this.changeDetector.markForCheck();
      }
      return;
    }

    await this.submitWorkFollow(references, key);
  }

  private workFollowKeyFor(items: readonly CatalogBookReference[]): string | null {
    const workIds = [...new Set(
      items
        .map(item => item.workId?.trim().toLowerCase())
        .filter((workId): workId is string => Boolean(workId)),
    )];
    return workIds.length ? `Work:${workIds.join('|')}` : null;
  }

  private referenceTitleKey(item: CatalogBookReference): string {
    const title = item.title?.trim() || this.submittedQuery.trim();
    return title
      .normalize('NFD')
      .replace(/[\u0300-\u036f]/g, '')
      .toLowerCase()
      .replace(/\s+/g, ' ');
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

  private async submitWorkFollow(
    items: readonly CatalogBookReference[],
    key: string,
  ): Promise<void> {
    this.referenceFollowPending = key;
    try {
      const token = await this.auth.getApiAccessToken();
      let addedCount = 0;
      for (const item of items) {
        const request = this.followRequest(item, 'Work');
        if (!request) {
          continue;
        }

        await firstValueFrom(this.memberApi.addWatchlistItem(token, request));
        addedCount++;
      }

      this.referenceFollowMessage = addedCount === 1
        ? 'Le titre a été ajouté à votre liste de recherche.'
        : 'Les éditions ont été ajoutées à votre liste de recherche.';
    } catch (error: unknown) {
      this.referenceFollowError = error instanceof HttpErrorResponse && error.status === 409
        ? 'Un de ces titres est déjà présent dans votre liste de recherche, ou votre liste est pleine.'
        : 'Les titres n’ont pas pu être ajoutés. Réessayez dans un instant.';
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
    const trimmedQuery = this.submittedQuery.trim();
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

  private loadLocalResults(): void {
    const loadVersion = ++this.localLoadVersion;
    this.loading = true;
    this.error = false;
    this.changeDetector.markForCheck();
    this.api.search(this.searchParams())
      .pipe(
        catchError(() => {
          if (loadVersion === this.localLoadVersion) {
            this.error = true;
          }
          return of(null);
        }),
        takeUntil(this.destroyed),
      )
      .subscribe(response => {
        if (loadVersion !== this.localLoadVersion) {
          return;
        }

        this.response = response;
        this.loading = false;
        this.changeDetector.markForCheck();
      });
  }

  private loadExternalResults(): void {
    const loadVersion = ++this.externalLoadVersion;
    const referenceQuery = this.submittedQuery.trim();
    if (referenceQuery.length < 2) {
      this.externalResponse = null;
      this.externalLoading = false;
      this.externalError = false;
      this.changeDetector.markForCheck();
      return;
    }

    this.externalLoading = true;
    this.externalError = false;
    this.changeDetector.markForCheck();
    this.api.searchReferences(referenceQuery, this.currentReferencePage, 20)
      .pipe(
        catchError(() => {
          if (loadVersion === this.externalLoadVersion) {
            this.externalError = true;
          }
          return of(null);
        }),
        takeUntil(this.destroyed),
      )
      .subscribe(response => {
        if (loadVersion !== this.externalLoadVersion) {
          return;
        }

        this.externalResponse = response;
        this.externalLoading = false;
        this.changeDetector.markForCheck();
      });
  }

  private searchParams(): CatalogSearchParams {
    return {
      query: this.submittedQuery,
      genre: this.genre,
      availability: this.availability,
      rareOnly: this.rareOnly,
      sort: this.sort,
      page: this.currentPage,
      pageSize: 24,
    };
  }

  private queryParams(
    includePagination = false,
    query = this.query,
  ): Record<string, string | number | boolean> {
    const params: Record<string, string | number | boolean> = {};
    if (query.trim()) params['q'] = query.trim();
    if (this.genre.trim()) params['genre'] = this.genre.trim();
    if (this.availability !== 'all') params['availability'] = this.availability;
    if (this.rareOnly) params['rare'] = true;
    if (this.sort !== 'relevance') params['sort'] = this.sort;
    if (includePagination) {
      if (this.currentPage > 1) params['page'] = this.currentPage;
      if (this.currentReferencePage > 1) params['referencePage'] = this.currentReferencePage;
    }
    return params;
  }

  private openSortMenu(): void {
    if (this.sortMenuOpen) {
      return;
    }

    this.sortMenuActiveIndex = this.sortIndex();
    this.sortMenuOpen = true;
    this.changeDetector.markForCheck();
  }

  private closeSortMenu(returnFocus = false): void {
    if (!this.sortMenuOpen) {
      return;
    }

    this.sortMenuOpen = false;
    this.changeDetector.markForCheck();
    if (returnFocus) {
      this.elementRef.nativeElement.querySelector<HTMLButtonElement>('.sort-select-trigger')?.focus();
    }
  }

  private sortIndex(): number {
    const index = this.sortChoices.findIndex(choice => choice.value === this.sort);
    return index >= 0 ? index : 0;
  }

  private moveSortMenuFocus(delta: number): void {
    const optionCount = this.sortChoices.length;
    this.sortMenuActiveIndex = (this.sortMenuActiveIndex + delta + optionCount) % optionCount;
    this.focusSortOption(this.sortMenuActiveIndex);
  }

  private focusSortOption(index: number): void {
    const optionCount = this.sortChoices.length;
    this.sortMenuActiveIndex = (index + optionCount) % optionCount;
    setTimeout(() => {
      if (!this.sortMenuOpen) {
        return;
      }

      this.elementRef.nativeElement
        .querySelector<HTMLButtonElement>(`#catalog-sort-option-${this.sortMenuActiveIndex}`)
        ?.focus();
    });
  }

  private readAvailability(value: string | null): CatalogAvailability {
    return value === 'available' || value === 'next' ? value : 'all';
  }

  private readRouteState(params: ParamMap): CatalogSearchRouteState {
    return {
      query: params.get('q') || '',
      genre: params.get('genre') || '',
      availability: this.readAvailability(params.get('availability')),
      rareOnly: params.get('rare') === 'true',
      sort: params.get('sort') === 'recent' ? 'recent' : 'relevance',
      page: this.readPositivePage(params.get('page')),
      referencePage: this.readPositivePage(params.get('referencePage')),
    };
  }

  private readPositivePage(value: string | null): number {
    const page = Number(value || '1');
    return Number.isInteger(page) && page > 0 ? page : 1;
  }

  private localRouteStateChanged(
    previous: CatalogSearchRouteState,
    next: CatalogSearchRouteState,
  ): boolean {
    return previous.query !== next.query ||
      previous.genre !== next.genre ||
      previous.availability !== next.availability ||
      previous.rareOnly !== next.rareOnly ||
      previous.sort !== next.sort ||
      previous.page !== next.page;
  }

  private externalRouteStateChanged(
    previous: CatalogSearchRouteState,
    next: CatalogSearchRouteState,
  ): boolean {
    return previous.query !== next.query || previous.referencePage !== next.referencePage;
  }

  private referenceReturnUrl(): string {
    const query = this.queryParams(true, this.submittedQuery);
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
    title: string | null;
    authors: string | null;
    publisher: string | null;
    publicationYear: number | null;
    coverUrl: string | null;
  } | null {
    const workId = item.workId?.trim();
    const isbn13 = item.isbn13?.trim();

    if (scope === 'Work' && workId) {
      return {
        scope: 'Work',
        workId,
        isbn13: null,
        title: item.title,
        authors: item.authors,
        publisher: item.publisher,
        publicationYear: item.publicationYear,
        coverUrl: item.coverUrl,
      };
    }

    if (scope === 'Edition' && isbn13) {
      return {
        scope: 'Edition',
        workId: null,
        isbn13,
        title: item.title,
        authors: item.authors,
        publisher: item.publisher,
        publicationYear: item.publicationYear,
        coverUrl: item.coverUrl,
      };
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
    if (pending.scope === 'Work') {
      const key = this.workFollowKeyFor(pending.items);
      if (key) {
        await this.submitWorkFollow(pending.items, key);
      }
      return;
    }

    const item = pending.items[0];
    const key = this.referenceFollowKey(item, pending.scope);
    if (key) {
      await this.submitReferenceFollow(item, pending.scope, key);
    }
  }

  private savePendingReferenceFollow(
    items: readonly CatalogBookReference[],
    scope: CatalogWatchlistScope,
  ): void {
    try {
      if (typeof sessionStorage !== 'undefined') {
        sessionStorage.setItem(
          this.pendingReferenceStorageKey,
          JSON.stringify({items, scope}),
        );
      }
    } catch {
      // A blocked session storage should not prevent the normal login redirect.
    }
  }

  private readPendingReferenceFollow(): {
    items: CatalogBookReference[];
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
        items?: CatalogBookReference[];
        item?: CatalogBookReference;
        scope?: CatalogWatchlistScope;
      };
      const items = Array.isArray(pending.items)
        ? pending.items
        : pending.item
          ? [pending.item]
          : [];
      if (!items.length || (pending.scope !== 'Work' && pending.scope !== 'Edition')) {
        this.clearPendingReferenceFollow();
        return null;
      }

      return {items, scope: pending.scope};
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
