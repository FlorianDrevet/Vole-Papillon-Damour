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
import {Observable, Subject, catchError, firstValueFrom, of, takeUntil} from 'rxjs';

import {CatalogApiService} from '../../core/catalog-api.service';
import {catalogSearchSidebarGenres} from '../../core/catalog-genres';
import {
  CatalogAvailability,
  CatalogBook,
  CatalogBookReference,
  CatalogSearchParams,
  CatalogSearchResponse,
  CatalogSort,
  CatalogReferenceSearchResponse,
  CatalogRareBookPage,
  CatalogWatchlistItem,
  CatalogWatchlistScope,
} from '../../core/catalog.models';
import {CatalogAuthService} from '../../core/catalog-auth.service';
import {CatalogMemberApiService} from '../../core/catalog-member-api.service';

interface CatalogSearchRouteState {
  query: string;
  genre: string;
  availability: CatalogAvailability;
  view: CatalogSearchView;
  rareOnly: boolean;
  includeExhausted: boolean;
  sort: CatalogSort;
  page: number;
  referencePage: number;
}

type CatalogSearchView = 'list' | 'grid';

type CatalogReferenceFollowScope = Extract<CatalogWatchlistScope, 'Work' | 'Edition'>;

interface CatalogReferenceFollowTarget {
  item: CatalogBookReference;
  scope: CatalogReferenceFollowScope;
}

interface CatalogGenreFilterOption {
  name: string;
  bookCount: number | null;
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
  availability: CatalogAvailability = 'available';
  rareOnly = false;
  includeExhausted = false;
  sort: CatalogSort = 'relevance';
  sortMenuOpen = false;
  sortMenuActiveIndex = 0;
  mobileFiltersOpen = false;
  browseMode = false;
  view: CatalogSearchView = 'list';
  loading = true;
  error = false;
  response: CatalogSearchResponse | null = null;
  rareResponse: CatalogRareBookPage | null = null;
  externalLoading = false;
  externalError = false;
  externalResponse: CatalogReferenceSearchResponse | null = null;
  referenceFollowPending: string | null = null;
  referenceFollowMessage: string | null = null;
  referenceFollowError: string | null = null;
  bookFollowErrorKey: string | null = null;
  bookFollowError: string | null = null;
  authPromptOpen = false;
  genreDialogOpen = false;
  genreSearchTerm = '';
  draftGenre = '';
  genreFavorites: string[] = [];
  genreFavoritePending: string | null = null;
  genreFavoriteError: string | null = null;
  genreFavoriteMessage: string | null = null;
  authPromptTitle = 'Connectez-vous pour suivre ce livre';
  authPromptDescription = 'Créez votre espace gratuit pour ajouter ce titre à votre liste de suivi et être prévenu dès qu’il arrive à la bourse aux livres.';
  authPromptBenefits: readonly string[] = [
    'Ajoutez autant de livres que vous voulez à votre liste de suivi',
    'Recevez une alerte dès qu’une édition suivie arrive au catalogue',
    'Accès en un instant, sans mot de passe à retenir',
  ];
  authPromptHint = 'Le catalogue reste consultable sans compte : seul l’ajout à votre liste de suivi nécessite une connexion.';

  private readonly followedReferenceKeys = new Set<string>();
  private pendingAuthPrompt: {targets: readonly CatalogReferenceFollowTarget[]} | null = null;
  private pendingAuthGenreFavorite: string | null = null;

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
    {
      value: 'price-desc',
      label: 'Prix décroissant',
      description: 'Les exemplaires rares les plus chers en premier',
    },
    {
      value: 'price-asc',
      label: 'Prix croissant',
      description: 'Les exemplaires rares les moins chers en premier',
    },
  ];

  private readonly destroyed = new Subject<void>();
  private readonly pendingReferenceStorageKey = 'vpd.catalog.pending-reference-follow';
  private readonly pendingGenreFavoriteStorageKey = 'vpd.catalog.pending-genre-favorite';
  private routeState: CatalogSearchRouteState | null = null;
  private currentPage = 1;
  private currentReferencePage = 1;
  private localLoadVersion = 0;
  private externalLoadVersion = 0;
  private localCatalogueIsbns = new Set<string>();

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
        this.view = nextState.view;
        this.rareOnly = nextState.rareOnly;
        this.includeExhausted = nextState.includeExhausted;
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
    void this.initializeReferenceFollowState();
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

  selectView(view: CatalogSearchView): void {
    if (this.rareOnly || this.view === view) {
      return;
    }

    this.view = view;
    this.changeDetector.markForCheck();
    void this.router.navigate([this.browseMode ? '/catalogue' : '/recherche'], {
      queryParams: this.queryParams(true, this.submittedQuery),
    });
  }

  sortLabel(): string {
    return this.availableSortChoices().find(choice => choice.value === this.sort)?.label
      ?? (this.rareOnly ? 'Prix décroissant' : 'Pertinence');
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
    this.sortMenuActiveIndex = this.availableSortChoices().findIndex(choice => choice.value === value);
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
        this.selectSort(this.availableSortChoices()[index].value);
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
        this.sortMenuActiveIndex = this.availableSortChoices().length - 1;
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

  @HostListener('document:keydown.escape', ['$event'])
  closeGenreDialogOnEscape(event: Event): void {
    if (this.genreDialogOpen) {
      event.preventDefault();
      this.closeGenreDialog();
    }
  }

  clearFilters(mobileFiltersTrigger?: HTMLButtonElement): void {
    const wasOpen = this.mobileFiltersOpen;
    this.query = '';
    this.genre = '';
    this.availability = this.defaultAvailability();
    this.rareOnly = false;
    this.includeExhausted = false;
    this.sort = 'relevance';
    this.mobileFiltersOpen = false;
    if (wasOpen) {
      mobileFiltersTrigger?.focus();
    }
    this.applyFilters();
  }

  toggleMobileFilters(trigger?: HTMLButtonElement): void {
    this.mobileFiltersOpen = !this.mobileFiltersOpen;
    if (!this.mobileFiltersOpen) {
      trigger?.focus();
    }
  }

  activeMobileFilterCount(): number {
    return Number(Boolean(this.genre))
      + Number(this.availability !== this.defaultAvailability())
      + Number(this.rareOnly);
  }

  mobileFilterSummary(): string {
    if (this.genre) {
      return this.genre;
    }

    switch (this.availability) {
      case 'next':
        return 'Prochaine bourse';
      case 'all':
        return 'Toutes les disponibilités';
      default:
        return 'Disponible maintenant';
    }
  }

  defaultAvailability(): CatalogAvailability {
    return this.browseMode ? 'all' : 'available';
  }

  goToPage(page: number): void {
    if (page < 1 || !this.response) {
      return;
    }

    if (this.response.totalCount === null) {
      if (page > this.response.page + 1 || (page > this.response.page && !this.response.hasMore)) {
        return;
      }
    } else if (page > this.totalPages()) {
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
    scope: CatalogReferenceFollowScope = 'Edition',
  ): Promise<void> {
    const key = this.referenceFollowKey(item, scope);
    if (!key || this.referenceFollowPending || this.followedReferenceKeys.has(key)) {
      return;
    }

    this.referenceFollowMessage = null;
    this.referenceFollowError = null;

    if (!this.auth.isAuthenticated()) {
      this.resetAuthPromptCopy();
      this.pendingAuthPrompt = {targets: [{item, scope}]};
      this.authPromptOpen = true;
      this.changeDetector.markForCheck();
      return;
    }

    await this.submitReferenceFollow(item, scope, key);
  }

  async followBook(book: CatalogBook): Promise<void> {
    const reference = this.bookFollowReference(book);
    const scope = this.bookFollowScope(book);
    const key = this.referenceFollowKey(reference, scope);
    this.bookFollowErrorKey = key;
    this.bookFollowError = null;

    await this.followReference(reference, scope);
  }

  bookFollowReference(book: CatalogBook): CatalogBookReference {
    return {
      isbn13: book.isbn13,
      workId: book.workId,
      title: book.title,
      authors: book.authors,
      publisher: book.publisher,
      publicationYear: book.publicationYear,
      coverUrl: book.coverUrl,
      source: 'catalog',
    };
  }

  bookFollowScope(book: CatalogBook): CatalogReferenceFollowScope {
    return book.workId?.trim() ? 'Work' : 'Edition';
  }

  bookFollowKey(book: CatalogBook): string | null {
    return this.referenceFollowKey(this.bookFollowReference(book), this.bookFollowScope(book));
  }

  bookFollowErrorFor(book: CatalogBook): string | null {
    return this.bookFollowKey(book) === this.bookFollowErrorKey ? this.bookFollowError : null;
  }

  referenceFollowKey(item: CatalogBookReference, scope: CatalogWatchlistScope): string | null {
    const target = scope === 'Work'
      ? item.workId?.trim().toLowerCase()
      : this.normalizeIsbn(item.isbn13);
    return target ? `${scope}:${target}` : null;
  }

  referenceFollowed(item: CatalogBookReference, scope: CatalogWatchlistScope = 'Edition'): boolean {
    const key = this.referenceFollowKey(item, scope);
    if (key && this.followedReferenceKeys.has(key)) {
      return true;
    }

    const workKey = scope === 'Edition' && item.workId
      ? this.referenceFollowKey(item, 'Work')
      : null;
    return workKey !== null && this.followedReferenceKeys.has(workKey);
  }

  hasSearchFollowTargets(): boolean {
    return this.searchFollowTargets().length > 0;
  }

  private searchFollowTargets(): CatalogReferenceFollowTarget[] {
    const items = this.visibleExternalReferences();
    const seenKeys = new Set<string>();
    const targets: CatalogReferenceFollowTarget[] = [];
    for (const item of items) {
      if (!item.workId?.trim()) {
        continue;
      }

      const key = this.referenceFollowKey(item, 'Work');
      if (!key || seenKeys.has(key)) {
        continue;
      }

      seenKeys.add(key);
      targets.push({item, scope: 'Work'});
    }

    return targets;
  }

  searchFollowKey(): string | null {
    return this.searchFollowKeyFor(this.searchFollowTargets());
  }

  searchFollowPending(): boolean {
    const key = this.searchFollowKey();
    return key !== null && this.referenceFollowPending === key;
  }

  searchFollowed(): boolean {
    const targets = this.searchFollowTargets();
    return targets.length > 0 && targets.every(target => {
      const key = this.referenceFollowKey(target.item, target.scope);
      return key !== null && this.followedReferenceKeys.has(key);
    });
  }

  async followSearchResults(): Promise<void> {
    const targets = this.searchFollowTargets();
    const key = this.searchFollowKeyFor(targets);
    if (!key || this.referenceFollowPending || this.searchFollowed()) {
      return;
    }

    this.referenceFollowMessage = null;
    this.referenceFollowError = null;

    if (!this.auth.isAuthenticated()) {
      this.pendingAuthPrompt = {targets};
      this.authPromptOpen = true;
      this.changeDetector.markForCheck();
      return;
    }

    await this.submitSearchFollow(targets, key);
  }

  closeAuthPrompt(): void {
    this.authPromptOpen = false;
    this.pendingAuthPrompt = null;
    this.pendingAuthGenreFavorite = null;
    this.resetAuthPromptCopy();
    this.changeDetector.markForCheck();
  }

  async confirmAuthPrompt(mode: 'login' | 'register'): Promise<void> {
    const pending = this.pendingAuthPrompt;
    const pendingGenre = this.pendingAuthGenreFavorite;
    this.authPromptOpen = false;
    this.pendingAuthPrompt = null;
    this.pendingAuthGenreFavorite = null;
    if (!pending && !pendingGenre) {
      return;
    }

    if (pending) {
      this.savePendingReferenceFollow(pending.targets);
    }
    if (pendingGenre) {
      this.savePendingGenreFavorite(pendingGenre);
    }
    try {
      const returnUrl = this.referenceReturnUrl();
      if (mode === 'register') {
        await this.auth.register(returnUrl);
      } else {
        await this.auth.login(returnUrl);
      }
    } catch {
      if (pending) {
        this.clearPendingReferenceFollow();
      }
      if (pendingGenre) {
        this.clearPendingGenreFavorite();
      }
      const message = 'La connexion n’a pas pu être démarrée. Réessayez.';
      const bookTarget = pending?.targets.find(target => target.item.source === 'catalog');
      if (bookTarget) {
        this.bookFollowErrorKey = this.referenceFollowKey(bookTarget.item, bookTarget.scope);
        this.bookFollowError = message;
      } else if (pendingGenre) {
        this.genreFavoriteError = message;
      } else {
        this.referenceFollowError = message;
      }
      this.resetAuthPromptCopy();
      this.changeDetector.markForCheck();
    }
  }

  private searchFollowKeyFor(targets: readonly CatalogReferenceFollowTarget[]): string | null {
    const keys = [...new Set(
      targets
        .map(target => this.referenceFollowKey(target.item, target.scope))
        .filter((key): key is string => Boolean(key)),
    )];
    return keys.length ? `Search:${keys.join('|')}` : null;
  }

  private normalizeIsbn(value: string | null | undefined): string | null {
    const normalized = value?.replace(/[\s-]/g, '').trim().toLowerCase();
    return normalized || null;
  }

  private async submitReferenceFollow(
    item: CatalogBookReference,
    scope: CatalogWatchlistScope,
    key: string,
  ): Promise<void> {
    if (this.followedReferenceKeys.has(key)) {
      return;
    }

    this.referenceFollowPending = key;
    try {
      const token = await this.auth.getApiAccessToken();
      const request = this.followRequest(item, scope);
      if (!request) {
        const message = 'Cette référence ne permet pas ce type de suivi.';
        if (item.source === 'catalog') {
          this.bookFollowErrorKey = key;
          this.bookFollowError = message;
        } else {
          this.referenceFollowError = message;
        }
        return;
      }

      await firstValueFrom(this.memberApi.addWatchlistItem(token, request));
      this.followedReferenceKeys.add(key);
      this.referenceFollowMessage = item.source === 'catalog'
        ? null
        : scope === 'Work'
          ? 'Le titre a été ajouté à vos livres suivis.'
          : 'L’édition a été ajoutée à vos livres suivis.';
    } catch (error: unknown) {
      const message = error instanceof HttpErrorResponse && error.status === 409
        ? 'Ce titre est déjà dans vos livres suivis, ou la liste est pleine.'
        : 'Le titre n’a pas pu être ajouté. Réessayez dans un instant.';
      if (item.source === 'catalog') {
        this.bookFollowErrorKey = key;
        this.bookFollowError = message;
      } else {
        this.referenceFollowError = message;
      }
    } finally {
      this.referenceFollowPending = null;
      this.changeDetector.markForCheck();
    }
  }

  private async submitSearchFollow(
    targets: readonly CatalogReferenceFollowTarget[],
    key: string,
  ): Promise<void> {
    this.referenceFollowPending = key;
    try {
      const token = await this.auth.getApiAccessToken();
      let addedCount = 0;
      for (const target of targets) {
        const request = this.followRequest(target.item, target.scope);
        const targetKey = this.referenceFollowKey(target.item, target.scope);
        if (!request || !targetKey || this.followedReferenceKeys.has(targetKey)) {
          continue;
        }

        await firstValueFrom(this.memberApi.addWatchlistItem(token, request));
        this.followedReferenceKeys.add(targetKey);
        addedCount++;
      }

      if (addedCount > 0) {
        this.referenceFollowMessage = targets.some(target => target.item.source === 'catalog')
          ? null
          : addedCount === 1
            ? 'Le titre a été ajouté à vos livres suivis.'
            : 'Les titres ont été ajoutés à vos livres suivis.';
      }
    } catch (error: unknown) {
      const message = error instanceof HttpErrorResponse && error.status === 409
        ? 'Un de ces titres est déjà dans vos livres suivis, ou la liste est pleine.'
        : 'Les titres n’ont pas pu être ajoutés. Réessayez dans un instant.';
      const bookTarget = targets.find(target => target.item.source === 'catalog');
      if (bookTarget) {
        this.bookFollowErrorKey = key;
        this.bookFollowError = message;
      } else {
        this.referenceFollowError = message;
      }
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
      return this.rareOnly
        ? `Livres rares pour « ${trimmedQuery} »`
        : `Résultats pour « ${trimmedQuery} »`;
    }

    return this.rareOnly
      ? 'Tous les livres rares'
      : this.browseMode ? 'Le catalogue complet' : 'Tous les livres du catalogue';
  }

  isNumericIsbnSearch(): boolean {
    return /^[0-9]+$/.test(this.submittedQuery.trim());
  }

  localCountLabel(): string {
    if (this.rareOnly) {
      const total = this.rareResponse?.totalCount ?? 0;
      return `${total} ${total === 1 ? 'livre rare' : 'livres rares'}`;
    }

    const response = this.response;
    if (!response) {
      return '0 éditions';
    }

    if (response.totalCount === null) {
      if (response.hasMore) {
        return `Plus de ${response.page * response.pageSize} éditions`;
      }
      if (response.books.length === 0 && response.page > 1) {
        return `Page ${response.page}`;
      }
      const lowerBound = (response.page - 1) * response.pageSize + response.books.length;
      return `${lowerBound} éditions`;
    }

    const total = response.totalCount;
    return `${total} ${total === 1 ? 'édition' : 'éditions'}`;
  }

  externalCountLabel(): string {
    const total = this.visibleExternalReferences().length;
    return `${total} ${total === 1 ? 'édition' : 'éditions'}`;
  }

  // The reference search is shared with administration, so exclude exact local editions here.
  visibleExternalReferences(): CatalogBookReference[] {
    return (this.externalResponse?.items ?? []).filter(reference => {
      const isbn13 = this.normalizeIsbn(reference.isbn13);
      return !isbn13 || !this.localCatalogueIsbns.has(isbn13);
    });
  }

  totalPages(): number {
    if (!this.response || this.response.totalCount === null || this.response.totalCount === 0) {
      return 1;
    }
    return Math.ceil(this.response.totalCount / this.response.pageSize);
  }

  pageNumbers(): number[] {
    if (!this.response || this.response.totalCount === null) {
      return this.response ? [this.response.page] : [];
    }

    const total = this.totalPages();
    const current = this.response?.page || 1;
    const first = Math.max(1, Math.min(current - 2, total - 4));
    const last = Math.min(total, first + 4);
    return Array.from({length: last - first + 1}, (_, index) => first + index);
  }

  resultSummary(): string {
    if (!this.rareOnly && this.response?.totalCount === null) {
      if (this.response.hasMore) {
        return `Plus de ${this.response.page * this.response.pageSize} titres trouvés dans la bourse aux livres`;
      }
      if (this.response.books.length === 0 && this.response.page > 1) {
        return `Aucun titre sur la page ${this.response.page}`;
      }
    }

    const total = this.rareOnly
      ? this.rareResponse?.totalCount ?? 0
      : this.response?.totalCount ?? 0;
    if (total === 0) {
      return this.rareOnly
        ? 'Aucun livre rare trouvé dans la bourse aux livres'
        : 'Aucun titre trouvé dans la bourse aux livres';
    }
    return this.rareOnly
      ? `${total} ${total === 1 ? 'livre rare trouvé' : 'livres rares trouvés'} dans la bourse aux livres`
      : `${total} ${total === 1 ? 'titre trouvé' : 'titres trouvés'} dans la bourse aux livres`;
  }

  availableGenres(): string[] {
    if (this.rareOnly) {
      return [];
    }

    return catalogSearchSidebarGenres(this.genre, this.genreFavorites);
  }

  openGenreDialog(): void {
    this.genreSearchTerm = '';
    this.draftGenre = this.genre;
    this.genreDialogOpen = true;
    this.changeDetector.markForCheck();
  }

  closeGenreDialog(): void {
    this.genreDialogOpen = false;
    this.genreSearchTerm = '';
    this.changeDetector.markForCheck();
  }

  filteredGenreOptions(): CatalogGenreFilterOption[] {
    const options: CatalogGenreFilterOption[] = this.response?.genreFacets?.length
      ? this.response.genreFacets.map(facet => ({name: facet.name, bookCount: facet.bookCount}))
      : (this.response?.genres ?? []).map(name => ({name, bookCount: null}));
    if (this.genre && !options.some(option => this.sameGenre(option.name, this.genre))) {
      options.push({name: this.genre, bookCount: null});
    }

    const query = this.normalizeGenreSearch(this.genreSearchTerm);
    return options
      .filter(option => !query || this.normalizeGenreSearch(option.name).includes(query))
      .sort((left, right) =>
        (right.bookCount ?? 0) - (left.bookCount ?? 0) || left.name.localeCompare(right.name, 'fr-FR'));
  }

  selectGenreOption(genre: string): void {
    this.draftGenre = genre;
    this.changeDetector.markForCheck();
  }

  applyGenreDialogSelection(): void {
    this.genre = this.draftGenre;
    this.closeGenreDialog();
    this.applyFilters();
  }

  isGenreFavorite(genre: string): boolean {
    return this.genreFavorites.some(favorite => this.sameGenre(favorite, genre));
  }

  async toggleGenreFavorite(genre: string): Promise<void> {
    this.genreFavoriteError = null;
    this.genreFavoriteMessage = null;

    if (!this.auth.isAuthenticated()) {
      this.pendingAuthGenreFavorite = genre;
      this.authPromptTitle = 'Connectez-vous pour épingler un genre';
      this.authPromptDescription = 'Enregistrez vos genres favoris sur votre compte pour les retrouver en tête des filtres du catalogue.';
      this.authPromptBenefits = [];
      this.authPromptHint = 'Votre compte conserve vos genres épinglés sur tous vos appareils.';
      this.authPromptOpen = true;
      this.changeDetector.markForCheck();
      return;
    }

    await this.setGenreFavorite(genre, !this.isGenreFavorite(genre));
  }

  trackBook(_index: number, isbn13: string): string {
    return isbn13;
  }

  trackReference(index: number, reference: CatalogBookReference): string {
    return `${reference.isbn13 || reference.workId || reference.title || 'reference'}-${index}`;
  }

  private loadLocalResults(): void {
    const loadVersion = ++this.localLoadVersion;
    this.localCatalogueIsbns.clear();
    this.loading = true;
    this.error = false;
    this.changeDetector.markForCheck();
    const result$: Observable<CatalogSearchResponse | CatalogRareBookPage> = this.rareOnly
      ? this.api.getPublicRareBooks({
        search: this.submittedQuery,
        includeSold: this.includeExhausted,
        sort: this.rareSort(),
        page: this.currentPage,
        pageSize: 24,
      })
      : this.api.search(this.searchParams());
    result$
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

        if (this.rareOnly) {
          const rareResponse = response as CatalogRareBookPage | null;
          this.rareResponse = rareResponse;
          this.response = rareResponse
            ? {
              generatedAt: rareResponse.generatedAt,
              books: [],
              totalCount: rareResponse.totalCount,
              hasMore: rareResponse.page * rareResponse.pageSize < rareResponse.totalCount,
              page: rareResponse.page,
              pageSize: rareResponse.pageSize,
              genres: [],
            }
            : null;
          this.localCatalogueIsbns = new Set(
            (rareResponse?.books ?? [])
              .map(book => this.normalizeIsbn(book.isbn13))
              .filter((isbn13): isbn13 is string => Boolean(isbn13)),
          );
        } else {
          const catalogResponse = response as CatalogSearchResponse | null;
          this.rareResponse = null;
          this.response = catalogResponse;
          this.localCatalogueIsbns = new Set(
            (catalogResponse?.books ?? [])
              .map(book => this.normalizeIsbn(book.isbn13))
              .filter((isbn13): isbn13 is string => Boolean(isbn13)),
          );
        }
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
      includeExhausted: this.includeExhausted,
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
    if (!this.rareOnly && this.genre.trim()) params['genre'] = this.genre.trim();
    if (!this.rareOnly && (!this.browseMode || this.availability !== 'all')) {
      params['availability'] = this.availability;
    }
    if (this.rareOnly) params['rare'] = true;
    if (this.includeExhausted) params['includeExhausted'] = true;
    if (this.sort !== (this.rareOnly ? 'price-desc' : 'relevance')) params['sort'] = this.sort;
    if (!this.rareOnly && this.view === 'list') params['view'] = this.view;
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
    const index = this.availableSortChoices().findIndex(choice => choice.value === this.sort);
    return index >= 0 ? index : 0;
  }

  private moveSortMenuFocus(delta: number): void {
    const optionCount = this.availableSortChoices().length;
    this.sortMenuActiveIndex = (this.sortMenuActiveIndex + delta + optionCount) % optionCount;
    this.focusSortOption(this.sortMenuActiveIndex);
  }

  private focusSortOption(index: number): void {
    const optionCount = this.availableSortChoices().length;
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
    if (value === 'available' || value === 'next' || value === 'all') {
      return value;
    }

    return this.defaultAvailability();
  }

  private readRouteState(params: ParamMap): CatalogSearchRouteState {
    const rareOnly = params.get('rare') === 'true';
    const rawSort = params.get('sort');
    return {
      query: params.get('q') || '',
      genre: params.get('genre') || '',
      availability: this.readAvailability(params.get('availability')),
      view: rareOnly || params.get('view') === 'list' ? 'list' : 'grid',
      rareOnly,
      includeExhausted: params.get('includeExhausted') === 'true',
      sort: rareOnly
        ? rawSort === 'price-asc' || rawSort === 'price-desc' || rawSort === 'recent'
          ? rawSort
          : 'price-desc'
        : rawSort === 'recent' ? 'recent' : 'relevance',
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
      previous.includeExhausted !== next.includeExhausted ||
      previous.sort !== next.sort ||
      previous.page !== next.page;
  }

  private externalRouteStateChanged(
    previous: CatalogSearchRouteState,
    next: CatalogSearchRouteState,
  ): boolean {
    return previous.query !== next.query || previous.referencePage !== next.referencePage;
  }

  availableSortChoices(): ReadonlyArray<{
    value: CatalogSort;
    label: string;
    description: string;
  }> {
    return this.rareOnly
      ? this.sortChoices.filter(choice => choice.value !== 'relevance')
      : this.sortChoices.filter(choice => choice.value === 'relevance' || choice.value === 'recent');
  }

  private rareSort(): 'recent' | 'price-asc' | 'price-desc' {
    return this.sort === 'price-asc' || this.sort === 'price-desc' || this.sort === 'recent'
      ? this.sort
      : 'price-desc';
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

  private async initializeReferenceFollowState(): Promise<void> {
    const pending = this.readPendingReferenceFollow();
    const pendingGenre = this.readPendingGenreFavorite();

    try {
      await this.auth.initialize();
    } catch {
      return;
    }

    if (!this.auth.isAuthenticated()) {
      return;
    }

    await Promise.all([
      this.loadReferenceFollowState(),
      this.loadGenreFavoriteState(),
    ]);

    if (pendingGenre) {
      this.clearPendingGenreFavorite();
      await this.setGenreFavorite(pendingGenre, true);
    }

    if (!pending) {
      return;
    }

    this.clearPendingReferenceFollow();
    const key = this.searchFollowKeyFor(pending.targets);
    if (key) {
      await this.submitSearchFollow(pending.targets, key);
    }
  }

  private async loadReferenceFollowState(): Promise<void> {
    try {
      const token = await this.auth.tryGetApiAccessToken();
      if (!token) {
        return;
      }

      const response = await firstValueFrom(this.memberApi.getWatchlist(token));
      for (const item of response.items) {
        const key = this.watchlistItemKey(item);
        if (key) {
          this.followedReferenceKeys.add(key);
        }
      }
    } catch {
      // A protected-list failure must not make the public catalogue unusable.
    } finally {
      this.changeDetector.markForCheck();
    }
  }

  private async loadGenreFavoriteState(): Promise<void> {
    try {
      const token = await this.auth.tryGetApiAccessToken();
      if (!token) {
        return;
      }

      const response = await firstValueFrom(this.memberApi.getGenreFavorites(token));
      this.genreFavorites = response.genres;
    } catch {
      // The public catalogue remains usable when member preferences are unavailable.
    } finally {
      this.changeDetector.markForCheck();
    }
  }

  private async setGenreFavorite(genre: string, pinned: boolean): Promise<void> {
    if (this.genreFavoritePending) {
      return;
    }

    this.genreFavoritePending = genre;
    this.genreFavoriteError = null;
    this.genreFavoriteMessage = null;
    this.changeDetector.markForCheck();
    try {
      const token = await this.auth.getApiAccessToken();
      const response = await firstValueFrom(pinned
        ? this.memberApi.addGenreFavorite(token, genre)
        : this.memberApi.removeGenreFavorite(token, genre));
      this.genreFavorites = response.genres;
      this.genreFavoriteMessage = pinned
        ? `${genre} est épinglé en tête de vos filtres.`
        : `${genre} a été retiré de vos genres épinglés.`;
    } catch {
      this.genreFavoriteError = 'La préférence de genre n’a pas pu être enregistrée. Réessayez.';
    } finally {
      this.genreFavoritePending = null;
      this.changeDetector.markForCheck();
    }
  }

  private sameGenre(left: string, right: string): boolean {
    return left.trim().toLocaleLowerCase('fr-FR') === right.trim().toLocaleLowerCase('fr-FR');
  }

  private normalizeGenreSearch(value: string): string {
    return value
      .trim()
      .normalize('NFD')
      .replace(/[\u0300-\u036f]/g, '')
      .toLocaleLowerCase('fr-FR');
  }

  private savePendingGenreFavorite(genre: string): void {
    try {
      if (typeof sessionStorage !== 'undefined') {
        sessionStorage.setItem(this.pendingGenreFavoriteStorageKey, genre);
      }
    } catch {
      // A blocked session storage should not prevent the normal login redirect.
    }
  }

  private readPendingGenreFavorite(): string | null {
    try {
      if (typeof sessionStorage === 'undefined') {
        return null;
      }

      const genre = sessionStorage.getItem(this.pendingGenreFavoriteStorageKey)?.trim();
      return genre || null;
    } catch {
      return null;
    }
  }

  private clearPendingGenreFavorite(): void {
    try {
      if (typeof sessionStorage !== 'undefined') {
        sessionStorage.removeItem(this.pendingGenreFavoriteStorageKey);
      }
    } catch {
      // The intent is best-effort when browser storage is unavailable.
    }
  }

  private resetAuthPromptCopy(): void {
    this.authPromptTitle = 'Connectez-vous pour suivre ce livre';
    this.authPromptDescription = 'Créez votre espace gratuit pour ajouter ce titre à votre liste de suivi et être prévenu dès qu’il arrive à la bourse aux livres.';
    this.authPromptBenefits = [
      'Ajoutez autant de livres que vous voulez à votre liste de suivi',
      'Recevez une alerte dès qu’une édition suivie arrive au catalogue',
      'Accès en un instant, sans mot de passe à retenir',
    ];
    this.authPromptHint = 'Le catalogue reste consultable sans compte : seul l’ajout à votre liste de suivi nécessite une connexion.';
  }

  private watchlistItemKey(item: CatalogWatchlistItem): string | null {
    const target = item.scope === 'Work'
      ? item.workId?.trim().toLowerCase()
      : this.normalizeIsbn(item.isbn13);
    return target ? `${item.scope}:${target}` : null;
  }

  private savePendingReferenceFollow(
    targets: readonly CatalogReferenceFollowTarget[],
  ): void {
    try {
      if (typeof sessionStorage !== 'undefined') {
        sessionStorage.setItem(
          this.pendingReferenceStorageKey,
          JSON.stringify({targets}),
        );
      }
    } catch {
      // A blocked session storage should not prevent the normal login redirect.
    }
  }

  private readPendingReferenceFollow(): {targets: CatalogReferenceFollowTarget[]} | null {
    try {
      if (typeof sessionStorage === 'undefined') {
        return null;
      }

      const raw = sessionStorage.getItem(this.pendingReferenceStorageKey);
      if (!raw) {
        return null;
      }

      const pending = JSON.parse(raw) as {
        targets?: Array<{item?: CatalogBookReference; scope?: CatalogWatchlistScope}>;
        items?: CatalogBookReference[];
        item?: CatalogBookReference;
        scope?: CatalogWatchlistScope;
      };
      const items = Array.isArray(pending.items)
        ? pending.items
        : pending.item
          ? [pending.item]
          : [];
      const targets = Array.isArray(pending.targets)
        ? pending.targets.flatMap(target =>
            target.item && (target.scope === 'Work' || target.scope === 'Edition')
              ? [{item: target.item, scope: target.scope}]
              : [],
          )
        : pending.scope === 'Work' || pending.scope === 'Edition'
          ? items.map(item => ({item, scope: pending.scope as CatalogReferenceFollowScope}))
          : [];
      if (!targets.length) {
        this.clearPendingReferenceFollow();
        return null;
      }

      return {targets};
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
