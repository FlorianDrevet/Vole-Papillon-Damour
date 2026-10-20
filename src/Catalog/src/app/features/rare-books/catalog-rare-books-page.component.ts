import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  ElementRef,
  HostListener,
  OnDestroy,
  OnInit,
} from '@angular/core';
import {ActivatedRoute, Router} from '@angular/router';
import {Subject, catchError, of, switchMap, takeUntil} from 'rxjs';

import {CatalogApiService} from '../../core/catalog-api.service';
import {
  CatalogRareBook,
  CatalogRareBookPage,
  CatalogRareBookFilters,
} from '../../core/catalog.models';

type RareBookSort = NonNullable<CatalogRareBookFilters['sort']>;

const EMPTY_PAGE: CatalogRareBookPage = {
  generatedAt: '',
  books: [],
  totalCount: 0,
  page: 1,
  pageSize: 24,
};

@Component({
  selector: 'app-catalog-rare-books-page',
  standalone: false,
  templateUrl: './catalog-rare-books-page.component.html',
  styleUrls: ['./catalog-rare-books-page.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CatalogRareBooksPageComponent implements OnInit, OnDestroy {
  books: CatalogRareBook[] = [];
  sort: RareBookSort = 'price-desc';
  sortMenuOpen = false;
  sortMenuActiveIndex = 0;
  page = 1;
  pageSize = 24;
  totalCount = 0;
  loading = true;
  error = false;

  readonly sortChoices: ReadonlyArray<{
    value: RareBookSort;
    label: string;
    description: string;
  }> = [
    {
      value: 'recent',
      label: 'Arrivée récente',
      description: 'Exemplaires ajoutés récemment',
    },
    {
      value: 'price-desc',
      label: 'Prix décroissant',
      description: 'Les exemplaires les plus chers en premier',
    },
    {
      value: 'price-asc',
      label: 'Prix croissant',
      description: 'Les exemplaires les moins chers en premier',
    },
  ];

  private readonly destroyed = new Subject<void>();

  constructor(
    private readonly route: ActivatedRoute,
    private readonly api: CatalogApiService,
    private readonly router: Router,
    private readonly changeDetector: ChangeDetectorRef,
    private readonly elementRef: ElementRef<HTMLElement>,
  ) {}

  ngOnInit(): void {
    this.route.queryParamMap
      .pipe(
        switchMap(params => {
          this.sort = parseSort(params.get('sort'));
          this.page = parsePage(params.get('page'));
          this.sortMenuOpen = false;
          this.sortMenuActiveIndex = this.sortIndex();
          this.loading = true;
          this.error = false;
          return this.api.getPublicRareBooks(this.filters()).pipe(
            catchError(() => {
              this.error = true;
              return of(EMPTY_PAGE);
            }),
          );
        }),
        takeUntil(this.destroyed),
      )
      .subscribe(page => {
        this.applyPage(page);
        this.changeDetector.markForCheck();
      });
  }

  ngOnDestroy(): void {
    this.destroyed.next();
    this.destroyed.complete();
  }

  sortLabel(): string {
    return this.sortChoices.find(choice => choice.value === this.sort)?.label ?? 'Prix décroissant';
  }

  toggleSortMenu(): void {
    if (this.sortMenuOpen) {
      this.closeSortMenu();
      return;
    }

    this.openSortMenu();
  }

  selectSort(value: RareBookSort): void {
    this.sort = value;
    this.page = 1;
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

  applyFilters(): void {
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: {
        ...(this.sort !== 'price-desc' ? {sort: this.sort} : {}),
        ...(this.page > 1 ? {page: this.page} : {}),
      },
    });
  }

  nextPage(): void {
    if (this.page < this.pageCount()) {
      this.page += 1;
      this.applyFilters();
    }
  }

  previousPage(): void {
    if (this.page > 1) {
      this.page -= 1;
      this.applyFilters();
    }
  }

  pageCount(): number {
    return Math.max(1, Math.ceil(this.totalCount / this.pageSize));
  }

  trackBook(_index: number, book: CatalogRareBook): string {
    return book.id;
  }

  private filters(): CatalogRareBookFilters {
    return {
      includeSold: false,
      sort: this.sort,
      page: this.page,
      pageSize: this.pageSize,
    };
  }

  private applyPage(page: CatalogRareBookPage): void {
    this.books = page.books;
    this.totalCount = page.totalCount;
    this.pageSize = page.pageSize;
    this.loading = false;
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
      this.elementRef.nativeElement.querySelector<HTMLButtonElement>('.rare-sort-trigger')?.focus();
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
        .querySelector<HTMLButtonElement>(`#rare-sort-option-${this.sortMenuActiveIndex}`)
        ?.focus();
    });
  }
}

function parsePage(value: string | null): number {
  const parsed = Number(value);
  return Number.isInteger(parsed) && parsed > 0 ? parsed : 1;
}

function parseSort(value: string | null): RareBookSort {
  return value === 'price-asc' || value === 'price-desc' || value === 'recent'
    ? value
    : 'price-desc';
}
