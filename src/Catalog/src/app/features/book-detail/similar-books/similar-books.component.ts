import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  Input,
  OnChanges,
  OnDestroy,
  ViewChild,
  signal,
} from '@angular/core';
import {EMPTY, Subscription, catchError} from 'rxjs';

import {CatalogApiService} from '../../../core/catalog-api.service';
import {CatalogSimilarBook} from '../../../core/catalog.models';

type SimilarBooksState = 'loading' | 'ready' | 'hidden';

@Component({
  selector: 'app-similar-books',
  standalone: false,
  templateUrl: './similar-books.component.html',
  styleUrls: ['./similar-books.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SimilarBooksComponent implements OnChanges, OnDestroy {
  @Input({required: true}) isbn13!: string;

  readonly state = signal<SimilarBooksState>('loading');
  readonly books = signal<CatalogSimilarBook[]>([]);
  readonly skeletons = [0, 1, 2, 3, 4];
  readonly canScrollPrevious = signal(false);
  readonly canScrollNext = signal(false);

  private request?: Subscription;
  private resizeObserver?: ResizeObserver;
  private carouselViewport?: HTMLDivElement;

  constructor(private readonly catalogApi: CatalogApiService) {}

  @ViewChild('bookList')
  set bookListElement(element: ElementRef<HTMLDivElement> | undefined) {
    this.resizeObserver?.disconnect();
    this.carouselViewport = element?.nativeElement;
    this.updateCarouselNavigation(this.carouselViewport);

    if (this.carouselViewport && typeof ResizeObserver !== 'undefined') {
      this.resizeObserver = new ResizeObserver(() =>
        this.updateCarouselNavigation(this.carouselViewport),
      );
      this.resizeObserver.observe(this.carouselViewport);
    }
  }

  ngOnChanges(): void {
    this.request?.unsubscribe();
    this.books.set([]);
    this.canScrollPrevious.set(false);
    this.canScrollNext.set(false);
    this.state.set('loading');
    this.request = this.catalogApi.getSimilarBooks(this.isbn13).pipe(
      catchError(() => {
        this.state.set('hidden');
        return EMPTY;
      }),
    ).subscribe(response => {
      if (response.books.length === 0) {
        this.state.set('hidden');
        return;
      }

      this.books.set(response.books);
      this.state.set('ready');
    });
  }

  ngOnDestroy(): void {
    this.request?.unsubscribe();
    this.resizeObserver?.disconnect();
  }

  updateCarouselNavigation(viewport: HTMLDivElement | undefined = this.carouselViewport): void {
    if (!viewport) {
      this.canScrollPrevious.set(false);
      this.canScrollNext.set(false);
      return;
    }

    const maxScrollLeft = Math.max(0, viewport.scrollWidth - viewport.clientWidth);
    this.canScrollPrevious.set(viewport.scrollLeft > 1);
    this.canScrollNext.set(viewport.scrollLeft < maxScrollLeft - 1);
  }

  scrollCarousel(direction: 'previous' | 'next', viewport: HTMLDivElement): void {
    const distance = viewport.clientWidth * 0.85;
    viewport.scrollBy({
      left: direction === 'next' ? distance : -distance,
      behavior: 'auto',
    });
  }
}
