import {ChangeDetectionStrategy, Component, Input, OnChanges, OnDestroy, signal} from '@angular/core';
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

  private request?: Subscription;

  constructor(private readonly catalogApi: CatalogApiService) {}

  ngOnChanges(): void {
    this.request?.unsubscribe();
    this.books.set([]);
    this.state.set('loading');
    this.request = this.catalogApi.getSimilarBooks(this.isbn13).pipe(
      catchError(() => {
        this.state.set('hidden');
        return EMPTY;
      }),
    ).subscribe(response => {
      if (response.books.length < 2) {
        this.state.set('hidden');
        return;
      }

      this.books.set(response.books);
      this.state.set('ready');
    });
  }

  ngOnDestroy(): void {
    this.request?.unsubscribe();
  }
}
