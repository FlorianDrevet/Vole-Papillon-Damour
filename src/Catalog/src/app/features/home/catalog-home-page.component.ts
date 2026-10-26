import {
  afterNextRender,
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  ElementRef,
  HostListener,
  OnInit,
  signal,
} from '@angular/core';
import {DomSanitizer, SafeResourceUrl} from '@angular/platform-browser';
import {ActivatedRoute, Router} from '@angular/router';
import {catchError, forkJoin, of} from 'rxjs';

import {CatalogApiService} from '../../core/catalog-api.service';
import {catalogSearchSidebarGenres, mergeCatalogGenres} from '../../core/catalog-genres';
import {CatalogBook, CatalogFair, CatalogRareBook, CatalogRareBookPage, CatalogSearchResponse} from '../../core/catalog.models';
import {calendarDataUri, calendarFilename} from '../../core/layouts/catalog-calendar';
import {CookieConsentService} from '../../shared/services/cookie-consent.service';

const HOME_BOOK_PAGE_SIZE = 8;

const EMPTY_SEARCH: CatalogSearchResponse = {
  generatedAt: '',
  books: [],
  totalCount: null,
  hasMore: false,
  page: 1,
  pageSize: HOME_BOOK_PAGE_SIZE,
  genres: [],
};

const EMPTY_RARE: CatalogRareBookPage = {
  generatedAt: '',
  books: [],
  totalCount: 0,
  page: 1,
  pageSize: 4,
};

interface HeroGenreChoice {
  value: string;
  label: string;
}

@Component({
  selector: 'app-catalog-home-page',
  standalone: false,
  templateUrl: './catalog-home-page.component.html',
  styleUrls: ['./catalog-home-page.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CatalogHomePageComponent implements OnInit {
  upcomingOnly = signal(false);
  search = '';
  heroGenre = '';
  heroGenreQuery = '';
  heroGenreMenuOpen = false;
  heroGenreMenuActiveIndex = 0;
  loading = signal(true);
  rareLoaded = signal(false);
  fairsLoaded = signal(false);
  hasLoadError = signal(false);
  recent = signal<CatalogBook[]>([]);
  recentTotal = signal<number | null>(null);
  recentHasMore = signal(false);
  catalogTotal = signal<number | null>(null);
  catalogHasMore = signal(false);
  catalogCountLoaded = signal(0);
  rare = signal<CatalogRareBook[]>([]);
  genres = signal<string[]>([]);
  readonly homeGenres = catalogSearchSidebarGenres();
  nextFair = signal<CatalogFair | null>(null);
  upcomingFairs = signal<CatalogFair[]>([]);
  private readonly mapsEmbedUrls = new Map<string, SafeResourceUrl>();

  constructor(
    private readonly route: ActivatedRoute,
    private readonly api: CatalogApiService,
    private readonly router: Router,
    private readonly sanitizer: DomSanitizer,
    private readonly changeDetector: ChangeDetectorRef,
    private readonly elementRef: ElementRef<HTMLElement>,
    public readonly consent: CookieConsentService,
  ) {
    afterNextRender(() => {
      if (!this.upcomingOnly()) {
        this.loadSupplementaryHomeContent();
      }
    });
  }

  ngOnInit(): void {
    this.upcomingOnly.set(this.route.snapshot.data['upcomingOnly'] === true);
    if (this.upcomingOnly()) {
      this.loadFairs();
      return;
    }

    forkJoin({
      recent: this.api.search({
        availability: 'available',
        sort: 'recent',
        pageSize: HOME_BOOK_PAGE_SIZE,
        includeTotalCount: true,
      }).pipe(catchError(() => {
        this.hasLoadError.set(true);
        return of(EMPTY_SEARCH);
      })),
      catalog: this.api.search({
        availability: 'all',
        includeExhausted: true,
        sort: 'recent',
        pageSize: HOME_BOOK_PAGE_SIZE,
        includeTotalCount: true,
      }).pipe(catchError(() => {
        this.hasLoadError.set(true);
        return of(EMPTY_SEARCH);
      })),
    }).subscribe(({recent, catalog}) => {
      this.recent.set(recent.books);
      this.recentTotal.set(recent.totalCount);
      this.recentHasMore.set(recent.hasMore);
      this.catalogTotal.set(catalog.totalCount);
      this.catalogHasMore.set(catalog.hasMore);
      this.catalogCountLoaded.set(catalog.books.length);
      this.genres.set(recent.genres ?? []);
      this.loading.set(false);
    });
  }

  submitSearch(): void {
    const query = this.search.trim();
    const genre = this.heroGenre.trim();
    void this.router.navigate(['/recherche'], {
      queryParams: {
        ...(query ? {q: query} : {}),
        ...(genre ? {genre} : {}),
      },
    });
  }

  availableGenres(): string[] {
    return mergeCatalogGenres(this.genres());
  }

  heroGenreChoices(): ReadonlyArray<HeroGenreChoice> {
    return [
      {value: '', label: 'Tous les genres'},
      ...this.availableGenres().map(genre => ({value: genre, label: genre})),
    ];
  }

  filteredHeroGenreChoices(): ReadonlyArray<HeroGenreChoice> {
    const query = this.normalizeGenreQuery(this.heroGenreQuery);
    if (!query) {
      return this.heroGenreChoices();
    }

    return this.heroGenreChoices().filter(choice =>
      this.normalizeGenreQuery(choice.label).includes(query));
  }

  onHeroGenreQueryChange(query: string): void {
    this.heroGenreQuery = query;
    const choices = this.filteredHeroGenreChoices();
    const selectedIndex = choices.findIndex(choice => choice.value === this.heroGenre);
    this.heroGenreMenuActiveIndex = selectedIndex >= 0 ? selectedIndex : (choices.length ? 0 : -1);
  }

  heroGenreLabel(): string {
    return this.heroGenre || 'Tous les genres';
  }

  toggleHeroGenreMenu(): void {
    if (this.heroGenreMenuOpen) {
      this.closeHeroGenreMenu();
      return;
    }

    this.openHeroGenreMenu();
  }

  selectHeroGenre(value: string): void {
    this.heroGenre = value;
    this.closeHeroGenreMenu(true);
    this.changeDetector.markForCheck();
  }

  handleHeroGenreTriggerKeydown(event: KeyboardEvent): void {
    switch (event.key) {
      case 'Enter':
      case ' ':
        event.preventDefault();
        this.toggleHeroGenreMenu();
        break;
      case 'ArrowDown':
        event.preventDefault();
        this.openHeroGenreMenu();
        this.moveHeroGenreFocus(1);
        break;
      case 'ArrowUp':
        event.preventDefault();
        this.openHeroGenreMenu();
        this.moveHeroGenreFocus(-1);
        break;
      case 'Escape':
        if (this.heroGenreMenuOpen) {
          event.preventDefault();
          this.closeHeroGenreMenu();
        }
        break;
    }
  }

  handleHeroGenreOptionKeydown(event: KeyboardEvent, index: number): void {
    const choices = this.filteredHeroGenreChoices();

    switch (event.key) {
      case 'Enter':
      case ' ':
        event.preventDefault();
        this.selectHeroGenre(choices[index].value);
        break;
      case 'ArrowDown':
        event.preventDefault();
        this.moveHeroGenreFocus(1);
        break;
      case 'ArrowUp':
        event.preventDefault();
        this.moveHeroGenreFocus(-1);
        break;
      case 'Home':
        event.preventDefault();
        this.focusHeroGenreOption(0);
        break;
      case 'End':
        event.preventDefault();
        this.focusHeroGenreOption(choices.length - 1);
        break;
      case 'Escape':
        event.preventDefault();
        event.stopPropagation();
        this.closeHeroGenreMenu(true);
        break;
      case 'Tab':
        this.closeHeroGenreMenu();
        break;
    }
  }

  handleHeroGenreSearchKeydown(event: KeyboardEvent): void {
    const choices = this.filteredHeroGenreChoices();

    switch (event.key) {
      case 'Enter':
        event.preventDefault();
        if (choices.length) {
          const index = this.heroGenreMenuActiveIndex >= 0 ? this.heroGenreMenuActiveIndex : 0;
          this.selectHeroGenre(choices[index].value);
        }
        break;
      case 'ArrowDown':
        if (choices.length) {
          event.preventDefault();
          this.focusHeroGenreOption(this.heroGenreMenuActiveIndex >= 0 ? this.heroGenreMenuActiveIndex : 0);
        }
        break;
      case 'ArrowUp':
        if (choices.length) {
          event.preventDefault();
          this.focusHeroGenreOption(this.heroGenreMenuActiveIndex >= 0 ? this.heroGenreMenuActiveIndex : choices.length - 1);
        }
        break;
      case 'Escape':
        event.preventDefault();
        event.stopPropagation();
        this.closeHeroGenreMenu(true);
        break;
    }
  }

  @HostListener('document:click', ['$event'])
  closeHeroGenreMenuOnDocumentClick(event: MouseEvent): void {
    if (this.heroGenreMenuOpen && !this.elementRef.nativeElement.contains(event.target as Node)) {
      this.closeHeroGenreMenu();
    }
  }

  showAll(): void {
    void this.router.navigate(['/catalogue'], {queryParams: {availability: 'available'}});
  }

  hasReliableRecentCount(): boolean {
    const total = this.recentTotal();
    return total !== null && (!this.recentHasMore() || total > this.recent().length);
  }

  hasReliableCatalogCount(): boolean {
    const total = this.catalogTotal();
    return total !== null && (!this.catalogHasMore() || total > this.catalogCountLoaded());
  }

  showRare(): void {
    void this.router.navigate(['/livres-rares']);
  }

  trackBook(_index: number, book: CatalogBook): string {
    return book.isbn13;
  }

  trackRareBook(_index: number, book: CatalogRareBook): string {
    return book.id;
  }

  formatDate(value: string, withYear = true): string {
    return new Intl.DateTimeFormat('fr-FR', {
      day: 'numeric',
      month: 'long',
      timeZone: 'Europe/Paris',
      ...(withYear ? {year: 'numeric'} : {}),
    }).format(new Date(value));
  }

  formatFairDateRange(fair: CatalogFair): string {
    const start = this.formatDate(fair.dateStart);
    if (!fair.dateEnd) {
      return start;
    }

    const end = this.formatDate(fair.dateEnd);
    return start === end ? start : `Du ${start} au ${end}`;
  }

  formatTime(value: string): string {
    return new Intl.DateTimeFormat('fr-FR', {
      hour: 'numeric',
      minute: '2-digit',
      // Legacy AssoEvents store civil opening hours in UTC components.
      timeZone: 'UTC',
    }).format(new Date(value)).replace(':', ' h ');
  }

  address(fair: CatalogFair): string {
    const street = [fair.roadNumber, fair.road].filter(Boolean).join(' ');
    const city = [fair.cityCode, fair.city].filter(Boolean).join(' ');
    return [street, city].filter(Boolean).join(', ');
  }

  mapsUrl(fair: CatalogFair): string {
    return `https://www.google.com/maps/search/?api=1&query=${encodeURIComponent(this.address(fair))}`;
  }

  mapsEmbedUrl(fair: CatalogFair): SafeResourceUrl {
    const cachedUrl = this.mapsEmbedUrls.get(fair.id);
    if (cachedUrl) {
      return cachedUrl;
    }

    const query = encodeURIComponent(this.address(fair));
    const safeUrl = this.sanitizer.bypassSecurityTrustResourceUrl(
      `https://www.google.com/maps?q=${query}&hl=fr&z=15&output=embed`,
    );
    this.mapsEmbedUrls.set(fair.id, safeUrl);
    return safeUrl;
  }

  enableMaps(): void {
    this.consent.enableMaps();
  }

  calendarLink(fair: CatalogFair): string {
    return calendarDataUri({...fair, location: this.address(fair)});
  }

  calendarFileName(fair: CatalogFair): string {
    return calendarFilename(fair.name);
  }

  trackFair(_index: number, fair: CatalogFair): string {
    return fair.id;
  }

  private loadFairs(): void {
    this.api.getUpcomingFairs().pipe(catchError(() => {
      this.hasLoadError.set(true);
      return of([] as CatalogFair[]);
    })).subscribe(fairs => {
      this.setFairs(fairs);
      this.loading.set(false);
    });
  }

  private loadSupplementaryHomeContent(): void {
    this.api.getPublicRareBooks({includeSold: false, sort: 'recent', pageSize: 4}).pipe(catchError(() => {
      this.hasLoadError.set(true);
      return of(EMPTY_RARE);
    })).subscribe(rare => {
      this.rare.set(rare.books);
      this.rareLoaded.set(true);
    });

    this.api.getUpcomingFairs().pipe(catchError(() => {
      this.hasLoadError.set(true);
      return of([] as CatalogFair[]);
    })).subscribe(fairs => this.setFairs(fairs));
  }

  private setFairs(fairs: CatalogFair[]): void {
    const sortedFairs = [...fairs].sort((a, b) =>
      new Date(a.dateStart).getTime() - new Date(b.dateStart).getTime());
    this.upcomingFairs.set(sortedFairs);
    this.nextFair.set(sortedFairs[0] ?? null);
    this.fairsLoaded.set(true);
  }

  private openHeroGenreMenu(): void {
    if (this.heroGenreMenuOpen) {
      return;
    }

    this.heroGenreQuery = '';
    this.heroGenreMenuActiveIndex = this.heroGenreIndex();
    this.heroGenreMenuOpen = true;
    this.changeDetector.markForCheck();
    setTimeout(() => {
      if (this.heroGenreMenuOpen) {
        this.elementRef.nativeElement.querySelector<HTMLInputElement>('.hero-genre-panel-search input')?.focus();
      }
    });
  }

  private closeHeroGenreMenu(returnFocus = false): void {
    if (!this.heroGenreMenuOpen) {
      return;
    }

    this.heroGenreMenuOpen = false;
    this.heroGenreQuery = '';
    this.heroGenreMenuActiveIndex = this.heroGenreIndex();
    this.changeDetector.markForCheck();
    if (returnFocus) {
      this.elementRef.nativeElement.querySelector<HTMLButtonElement>('.hero-genre-select-trigger')?.focus();
    }
  }

  private heroGenreIndex(): number {
    const index = this.heroGenreChoices().findIndex(choice => choice.value === this.heroGenre);
    return index >= 0 ? index : 0;
  }

  private moveHeroGenreFocus(delta: number): void {
    const optionCount = this.filteredHeroGenreChoices().length;
    if (!optionCount) {
      return;
    }

    this.focusHeroGenreOption((this.heroGenreMenuActiveIndex + delta + optionCount) % optionCount);
  }

  private focusHeroGenreOption(index: number): void {
    const optionCount = this.filteredHeroGenreChoices().length;
    if (!optionCount) {
      return;
    }

    this.heroGenreMenuActiveIndex = (index + optionCount) % optionCount;
    setTimeout(() => {
      if (!this.heroGenreMenuOpen) {
        return;
      }

      this.elementRef.nativeElement
        .querySelector<HTMLButtonElement>(`#catalog-home-genre-option-${this.heroGenreMenuActiveIndex}`)
        ?.focus();
    });
  }

  private normalizeGenreQuery(value: string): string {
    return value
      .normalize('NFD')
      .replace(/[\u0300-\u036f]/g, '')
      .toLocaleLowerCase('fr-FR')
      .trim();
  }
}
